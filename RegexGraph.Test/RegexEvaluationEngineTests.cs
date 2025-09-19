using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RegexNodeGraph;
using RegexNodeGraph.Evaluation;
using RegexNodeGraph.RegexRules;

namespace UnitTestProject1;

[TestClass]
public class RegexEvaluationEngineTests
{
    private static RegexEvaluationEngine CreateEngine() => new();

    [TestMethod]
    public void EvaluateAll_ComputesCoverageAndMetrics()
    {
        var rules = new[]
        {
            new RegexTransformationRule("pizza", "FOOD", ConfigOptions.NonUscireInCasoDiMatch)
            {
                Description = "Food rule"
            },
            new RegexTransformationRule("iban\\d+", "IBAN", ConfigOptions.NonUscireInCasoDiMatch)
            {
                Description = "Iban rule"
            }
        };

        var samples = new[]
        {
            new RegexSample("Ho mangiato una pizza", "t1"),
            new RegexSample("Bonifico iban1234", "t2")
        };

        var request = new RegexEvaluationRequest(rules, samples, recompileExpressions: false, useRegexCache: true);
        var engine = CreateEngine();

        var report = engine.EvaluateAll(request);

        Assert.AreEqual(2, report.RuleResults.Count);

        var firstRuleCoverage = report.RuleResults[0].Coverage;
        Assert.AreEqual(1, firstRuleCoverage.MatchCount);
        CollectionAssert.AreEqual(new[] { 0 }, firstRuleCoverage.MatchedSampleIndices.ToArray());
        Assert.IsTrue(firstRuleCoverage.MatchThroughput >= 0);
        Assert.IsTrue(firstRuleCoverage.EvalThroughput >= 0);

        var secondRuleCoverage = report.RuleResults[1].Coverage;
        Assert.AreEqual(1, secondRuleCoverage.MatchCount);
        CollectionAssert.AreEqual(new[] { 1 }, secondRuleCoverage.MatchedSampleIndices.ToArray());

        Assert.IsTrue(report.CoveredSamples[0]);
        Assert.IsTrue(report.CoveredSamples[1]);
        CollectionAssert.AreEqual(Array.Empty<int>(), report.GetUnmatchedSampleIndices().ToArray());

        var firstRuleFirstSample = report.RuleResults[0].SampleResults.First(r => r.Sample.Identifier == "t1");
        Assert.IsTrue(firstRuleFirstSample.IsMatch);
        Assert.AreEqual("Ho mangiato una FOOD", firstRuleFirstSample.Output);

        Assert.AreEqual(2, report.Metrics.SampleDurations.Count);
        Assert.AreEqual(0, report.Metrics.ErrorCount);
        Assert.AreEqual(4, report.Metrics.EvaluationsCount);
        Assert.AreEqual(2, report.Metrics.DistinctSamplesEvaluated);
        Assert.IsTrue(report.Metrics.OverallEvalThroughput >= 0);
    }

    [TestMethod]
    public void EvaluateAll_AddsCompilationErrorsToCoverage()
    {
        var rule = new RegexTransformationRule("abc", "x", ConfigOptions.NonUscireInCasoDiMatch)
        {
            Description = "Broken"
        };

        var samples = new[] { new RegexSample("abc", "s1") };

        var request = new RegexEvaluationRequest(new[] { rule }, samples, optionsOverride: (RegexOptions)int.MaxValue);
        var engine = CreateEngine();

        var report = engine.EvaluateAll(request);

        var coverage = report.RuleResults.Single().Coverage;
        Assert.IsTrue(coverage.HasError);
        Assert.IsInstanceOfType(coverage.Error, typeof(RegexCompilationException));
        Assert.AreEqual(0, coverage.MatchCount);
        Assert.AreEqual(1, coverage.TotalSamples);
        Assert.AreEqual(0d, coverage.MatchThroughput);
        Assert.AreEqual(0d, coverage.EvalThroughput);
    }

    [TestMethod]
    public async Task EvaluateAllAsync_ThrowsWhenCancelled()
    {
        var rule = new RegexTransformationRule("pizza", "FOOD", ConfigOptions.NonUscireInCasoDiMatch);
        var samples = Enumerable.Range(0, 5).Select(i => new RegexSample($"sample {i}")).ToList();
        var request = new RegexEvaluationRequest(new[] { rule }, samples);
        var engine = CreateEngine();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => engine.EvaluateAllAsync(request, cts.Token));
    }

    [TestMethod]
    public void EvaluateAll_UsesOptionsOverrideAndTimeout()
    {
        var rule = new RegexTransformationRule("pizza", "FOOD", ConfigOptions.NonUscireInCasoDiMatch);
        var samples = new[]
        {
            new RegexSample("PIZZA"),
            new RegexSample(new string('a', 2000))
        };

        var request = new RegexEvaluationRequest(
            new[] { rule },
            samples,
            optionsOverride: RegexOptions.None,
            matchTimeout: TimeSpan.FromMilliseconds(1));

        var engine = CreateEngine();
        var report = engine.EvaluateAll(request);

        var ruleResult = report.RuleResults.Single();
        var firstSample = ruleResult.SampleResults.First();
        Assert.IsFalse(firstSample.IsMatch, "Override should make the regex case-sensitive");

        var timeoutResult = ruleResult.SampleResults.Last();
        Assert.IsNotNull(timeoutResult.Error);
        Assert.IsInstanceOfType(timeoutResult.Error, typeof(RegexExecutionException));
        Assert.IsInstanceOfType(timeoutResult.Error!.InnerException, typeof(RegexMatchTimeoutException));
    }

    [TestMethod]
    public void EvaluateAll_ReusesPrecompiledRegexWhenDisabled()
    {
        var rule = new RegexTransformationRule("pizza", "FOOD", ConfigOptions.NonUscireInCasoDiMatch)
        {
            RegexFrom = new Regex("pizza", RegexOptions.None)
        };

        var samples = new[] { new RegexSample("PIZZA") };
        var request = new RegexEvaluationRequest(new[] { rule }, samples, recompileExpressions: false);
        var engine = CreateEngine();

        var report = engine.EvaluateAll(request);
        var sampleResult = report.RuleResults.Single().SampleResults.Single();

        Assert.IsFalse(sampleResult.IsMatch, "Existing regex should be reused with its original options");
    }

    [TestMethod]
    public void EvaluateAll_CanUseParallelismWithoutAffectingOrder()
    {
        var rules = Enumerable.Range(0, 10)
            .Select(i => new RegexTransformationRule($"rule{i}", "x", ConfigOptions.NonUscireInCasoDiMatch))
            .ToList();
        var samples = new[] { new RegexSample("rule5"), new RegexSample("rule7") };

        var request = new RegexEvaluationRequest(rules, samples, degreeOfParallelism: 4);
        var engine = CreateEngine();

        var report = engine.EvaluateAll(request);

        Assert.AreEqual(rules.Count, report.RuleResults.Count);
        for (int i = 0; i < rules.Count; i++)
        {
            Assert.AreEqual(i, report.RuleResults[i].Coverage.RuleIndex);
        }
    }

    [TestMethod]
    public void EvaluateAll_OverloadBuildsRequestFromTexts()
    {
        var rule = new RegexTransformationRule("pizza", "FOOD", ConfigOptions.NonUscireInCasoDiMatch);
        var engine = CreateEngine();

        var report = engine.EvaluateAll(new[] { rule }, new[] { "pizza" });

        var result = report.RuleResults.Single().SampleResults.Single();
        Assert.IsTrue(result.IsMatch);
        Assert.AreEqual("FOOD", result.Output);
    }

    [TestMethod]
    public void ThroughputCalculations_HandleZeroElapsed()
    {
        Assert.AreEqual(double.PositiveInfinity, RegexEvaluationEngine.CalculateMatchThroughput(TimeSpan.Zero, 1));
        Assert.AreEqual(double.PositiveInfinity, RegexEvaluationEngine.CalculateEvaluationThroughput(TimeSpan.Zero, 1));
        Assert.AreEqual(0d, RegexEvaluationEngine.CalculateMatchThroughput(TimeSpan.FromSeconds(1), 0));
        Assert.AreEqual(0d, RegexEvaluationEngine.CalculateEvaluationThroughput(TimeSpan.FromSeconds(1), 0));
    }

    [TestMethod]
    public void Report_GetUnmatchedSamplesReturnsExpectedIndices()
    {
        var rule = new RegexTransformationRule("pizza", "FOOD", ConfigOptions.NonUscireInCasoDiMatch);
        var samples = new[]
        {
            new RegexSample("pizza"),
            new RegexSample("pasta"),
            new RegexSample("pizza e pasta")
        };

        var request = new RegexEvaluationRequest(new[] { rule }, samples);
        var engine = CreateEngine();

        var report = engine.EvaluateAll(request);
        CollectionAssert.AreEqual(new[] { 1 }, report.GetUnmatchedSampleIndices().ToArray());
    }

    [TestMethod]
    public void Metrics_GetSlowestRulesReturnsSortedResults()
    {
        var coverage = new List<RuleCoverageResult>
        {
            new(0, new RegexTransformationRule("a", "", ConfigOptions.NonUscireInCasoDiMatch), 1, 2, TimeSpan.FromMilliseconds(10), 100, 200, new[] { 0 }),
            new(1, new RegexTransformationRule("b", "", ConfigOptions.NonUscireInCasoDiMatch), 0, 2, TimeSpan.FromMilliseconds(30), 0, 200, Array.Empty<int>())
        };
        var samples = new List<SampleMatchResult>
        {
            new(0, new RegexSample("a"), coverage[0].Rule, true, "", TimeSpan.FromMilliseconds(5)),
            new(1, new RegexSample("b"), coverage[1].Rule, false, "b", TimeSpan.FromMilliseconds(15))
        };

        var metrics = RegexEvaluationMetrics.BuildSummary(coverage, samples, TimeSpan.FromMilliseconds(40));

        Assert.AreEqual(2, metrics.GetSlowestRules(5).Count);
        Assert.AreEqual(1, metrics.GetSlowestRules(1).Single().RuleIndex);
        Assert.AreEqual(2, metrics.DistinctSamplesEvaluated);
        Assert.AreEqual(2, metrics.EvaluationsCount);
        Assert.IsTrue(metrics.OverallEvalThroughput > 0);
    }
}
