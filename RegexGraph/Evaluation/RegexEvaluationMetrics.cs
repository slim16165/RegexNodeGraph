using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RegexNodeGraph.Evaluation;

public sealed class RegexEvaluationMetrics
{
    public RegexEvaluationMetrics(
        TimeSpan totalDuration,
        TimeSpan coverageDuration,
        int errorCount,
        int evaluationsCount,
        int distinctSamplesEvaluated,
        double overallEvalThroughput,
        IReadOnlyList<RuleCoverageResult> slowestRules,
        IReadOnlyDictionary<int, TimeSpan> sampleDurations)
    {
        TotalDuration = totalDuration;
        CoverageDuration = coverageDuration;
        ErrorCount = errorCount;
        EvaluationsCount = evaluationsCount;
        DistinctSamplesEvaluated = distinctSamplesEvaluated;
        OverallEvalThroughput = overallEvalThroughput;
        SlowestRules = slowestRules;
        SampleDurations = sampleDurations;
    }

    public TimeSpan TotalDuration { get; }
    public TimeSpan CoverageDuration { get; }
    public int ErrorCount { get; }
    public int EvaluationsCount { get; }
    public int DistinctSamplesEvaluated { get; }
    public double OverallEvalThroughput { get; }
    public IReadOnlyList<RuleCoverageResult> SlowestRules { get; }
    public IReadOnlyDictionary<int, TimeSpan> SampleDurations { get; }

    public IReadOnlyList<RuleCoverageResult> GetSlowestRules(int top)
    {
        if (top <= 0) return Array.Empty<RuleCoverageResult>();
        return SlowestRules.Take(top).ToList();
    }

    public static RegexEvaluationMetrics BuildSummary(
        IEnumerable<RuleCoverageResult> coverageResults,
        IEnumerable<SampleMatchResult> sampleResults,
        TimeSpan totalDuration)
    {
        var coverageList = coverageResults.ToList();
        var sampleList = sampleResults.ToList();

        TimeSpan coverageDuration = TimeSpan.FromTicks(coverageList.Sum(r => r.Elapsed.Ticks));
        int errorCount = coverageList.Count(r => r.HasError) + sampleList.Count(r => r.Error is not null);
        int evaluationsCount = sampleList.Count;
        int distinctSamplesEvaluated = sampleList.Select(r => r.SampleIndex).Distinct().Count();
        double overallEvalThroughput = RegexEvaluationEngine.CalculateEvaluationThroughput(totalDuration, evaluationsCount);

        var slowestRules = coverageList
            .OrderByDescending(r => r.Elapsed)
            .ThenByDescending(r => r.MatchCount)
            .ToList();

        var sampleDurations = sampleList
            .GroupBy(r => r.SampleIndex)
            .ToDictionary(g => g.Key, g => TimeSpan.FromTicks(g.Sum(r => r.Elapsed.Ticks)));

        return new RegexEvaluationMetrics(
            totalDuration,
            coverageDuration,
            errorCount,
            evaluationsCount,
            distinctSamplesEvaluated,
            overallEvalThroughput,
            new ReadOnlyCollection<RuleCoverageResult>(slowestRules),
            new ReadOnlyDictionary<int, TimeSpan>(sampleDurations));
    }
}
