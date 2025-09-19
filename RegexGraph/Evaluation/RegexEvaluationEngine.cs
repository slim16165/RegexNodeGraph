using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using RegexNodeGraph.RegexRules;

namespace RegexNodeGraph.Evaluation;

public sealed class RegexEvaluationEngine : IRegexEvaluationEngine
{
    private readonly ConcurrentDictionary<RegexCacheKey, Regex> _regexCache = new();

    public RegexEvaluationReport EvaluateAll(RegexEvaluationRequest request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        return EvaluateAllAsync(request, CancellationToken.None).GetAwaiter().GetResult();
    }

    public RegexEvaluationReport EvaluateRule(RegexEvaluationRequest request, RegexTransformationRule rule)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (rule is null) throw new ArgumentNullException(nameof(rule));
        return EvaluateRuleAsync(request, rule, CancellationToken.None).GetAwaiter().GetResult();
    }

    public Task<RegexEvaluationReport> EvaluateAllAsync(RegexEvaluationRequest request, CancellationToken ct = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        return Task.FromResult(EvaluateAllCore(request, ct));
    }

    public Task<RegexEvaluationReport> EvaluateRuleAsync(RegexEvaluationRequest request, RegexTransformationRule rule, CancellationToken ct = default)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));
        if (rule is null) throw new ArgumentNullException(nameof(rule));
        return Task.FromResult(EvaluateRuleCore(request, rule, ct));
    }

    public RegexEvaluationReport EvaluateAll(IEnumerable<RegexTransformationRule> rules, IEnumerable<string> texts)
    {
        if (rules is null) throw new ArgumentNullException(nameof(rules));
        if (texts is null) throw new ArgumentNullException(nameof(texts));

        var request = RegexEvaluationRequest.FromTexts(rules, texts);
        return EvaluateAll(request);
    }

    public Task<RegexEvaluationReport> EvaluateAllAsync(IEnumerable<RegexTransformationRule> rules, IEnumerable<string> texts, CancellationToken ct = default)
    {
        if (rules is null) throw new ArgumentNullException(nameof(rules));
        if (texts is null) throw new ArgumentNullException(nameof(texts));

        var request = RegexEvaluationRequest.FromTexts(rules, texts);
        return EvaluateAllAsync(request, ct);
    }

    private RegexEvaluationReport EvaluateAllCore(RegexEvaluationRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var totalStopwatch = Stopwatch.StartNew();
        var ruleResults = new RuleEvaluationResult[request.Rules.Count];

        if (request.DegreeOfParallelism > 1)
        {
            var parallelOptions = new ParallelOptions
            {
                CancellationToken = ct,
                MaxDegreeOfParallelism = request.DegreeOfParallelism
            };

            Parallel.For(0, request.Rules.Count, parallelOptions, i =>
            {
                var rule = request.Rules[i];
                ruleResults[i] = EvaluateSingleRule(request, rule, i, ct);
            });
        }
        else
        {
            for (int i = 0; i < request.Rules.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var rule = request.Rules[i];
                ruleResults[i] = EvaluateSingleRule(request, rule, i, ct);
            }
        }

        totalStopwatch.Stop();
        var coveredSamples = BuildCoveredSamplesBitmap(request.Samples.Count, ruleResults);
        return new RegexEvaluationReport(ruleResults, coveredSamples, totalStopwatch.Elapsed);
    }

    private RegexEvaluationReport EvaluateRuleCore(RegexEvaluationRequest request, RegexTransformationRule rule, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        int ruleIndex = request.GetRuleIndex(rule);
        var ruleResult = EvaluateSingleRule(request, rule, ruleIndex, ct);
        var coveredSamples = BuildCoveredSamplesBitmap(request.Samples.Count, new[] { ruleResult });
        return new RegexEvaluationReport(new[] { ruleResult }, coveredSamples, ruleResult.Coverage.Elapsed);
    }

    private static BitArray BuildCoveredSamplesBitmap(int sampleCount, IEnumerable<RuleEvaluationResult> ruleResults)
    {
        var coveredSamples = new BitArray(sampleCount);
        foreach (var result in ruleResults)
        {
            foreach (var index in result.Coverage.MatchedSampleIndices)
            {
                coveredSamples[index] = true;
            }
        }

        return coveredSamples;
    }

    private RuleEvaluationResult EvaluateSingleRule(
        RegexEvaluationRequest request,
        RegexTransformationRule rule,
        int ruleIndex,
        CancellationToken ct)
    {
        var sampleResults = new List<SampleMatchResult>(request.Samples.Count);
        var matchedIndices = new List<int>();

        var ruleStopwatch = Stopwatch.StartNew();
        Regex? compiledRegex = null;
        Exception? compilationError = null;

        try
        {
            compiledRegex = CompileRegex(rule, request);
        }
        catch (Exception ex)
        {
            compilationError = new RegexCompilationException($"Failed to compile regex for rule '{rule.Description ?? rule.From}'", rule, ex);
        }

        if (compiledRegex is null)
        {
            ruleStopwatch.Stop();
            var coverageWithError = new RuleCoverageResult(
                ruleIndex,
                rule,
                0,
                request.Samples.Count,
                ruleStopwatch.Elapsed,
                0d,
                0d,
                matchedIndices,
                compilationError);
            return new RuleEvaluationResult(coverageWithError, Array.Empty<SampleMatchResult>());
        }

        for (int sampleIndex = 0; sampleIndex < request.Samples.Count; sampleIndex++)
        {
            ct.ThrowIfCancellationRequested();
            var sample = request.Samples[sampleIndex];
            var sampleStopwatch = Stopwatch.StartNew();
            bool isMatch = false;
            string output = sample.Text;
            Exception? executionError = null;

            try
            {
                var match = compiledRegex.Match(sample.Text);
                isMatch = match.Success;
                if (isMatch)
                {
                    output = compiledRegex.Replace(sample.Text, rule.To);
                    matchedIndices.Add(sampleIndex);
                }
            }
            catch (RegexMatchTimeoutException ex)
            {
                executionError = new RegexExecutionException("Regex execution timed out", rule, ex);
            }
            catch (Exception ex)
            {
                executionError = new RegexExecutionException("Regex execution failed", rule, ex);
            }

            sampleStopwatch.Stop();
            sampleResults.Add(new SampleMatchResult(sampleIndex, sample, rule, isMatch, output, sampleStopwatch.Elapsed, executionError));
        }

        ruleStopwatch.Stop();
        TimeSpan elapsed = ruleStopwatch.Elapsed;
        double matchThroughput = CalculateMatchThroughput(elapsed, matchedIndices.Count);
        double evaluationThroughput = CalculateEvaluationThroughput(elapsed, request.Samples.Count);

        var coverage = new RuleCoverageResult(
            ruleIndex,
            rule,
            matchedIndices.Count,
            request.Samples.Count,
            elapsed,
            matchThroughput,
            evaluationThroughput,
            matchedIndices,
            null);

        return new RuleEvaluationResult(coverage, sampleResults);
    }

    private Regex CompileRegex(RegexTransformationRule rule, RegexEvaluationRequest request)
    {
        if (!request.RecompileExpressions &&
            request.OptionsOverride is null &&
            request.MatchTimeout is null &&
            rule.RegexFrom is not null)
        {
            return rule.RegexFrom;
        }

        var options = request.OptionsOverride ?? rule.RegexFrom?.Options ?? RegexEvaluationRequest.DefaultRegexOptions;
        var timeout = request.MatchTimeout ?? rule.RegexFrom?.MatchTimeout ?? Regex.InfiniteMatchTimeout;
        var pattern = rule.From;

        if (request.UseRegexCache)
        {
            var key = new RegexCacheKey(pattern, options, timeout);
            return _regexCache.GetOrAdd(key, static k => new Regex(k.Pattern, k.Options, k.Timeout));
        }

        return new Regex(pattern, options, timeout);
    }

    internal static double CalculateMatchThroughput(TimeSpan elapsed, int matchCount)
    {
        if (matchCount == 0) return 0d;
        if (elapsed.TotalSeconds <= 0) return double.PositiveInfinity;
        return matchCount / elapsed.TotalSeconds;
    }

    internal static double CalculateEvaluationThroughput(TimeSpan elapsed, int evaluatedSamples)
    {
        if (evaluatedSamples == 0) return 0d;
        if (elapsed.TotalSeconds <= 0) return double.PositiveInfinity;
        return evaluatedSamples / elapsed.TotalSeconds;
    }

    private readonly struct RegexCacheKey : IEquatable<RegexCacheKey>
    {
        public RegexCacheKey(string pattern, RegexOptions options, TimeSpan timeout)
        {
            Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
            Options = options;
            Timeout = timeout;
        }

        public string Pattern { get; }
        public RegexOptions Options { get; }
        public TimeSpan Timeout { get; }

        public bool Equals(RegexCacheKey other)
        {
            return Options == other.Options && Timeout.Equals(other.Timeout) && string.Equals(Pattern, other.Pattern, StringComparison.Ordinal);
        }

        public override bool Equals(object? obj) => obj is RegexCacheKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(Pattern);
                hash = (hash * 31) + (int)Options;
                hash = (hash * 31) + Timeout.GetHashCode();
                return hash;
            }
        }
    }
}
