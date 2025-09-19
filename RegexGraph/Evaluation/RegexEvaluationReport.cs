using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RegexNodeGraph.Evaluation;

public sealed class RegexEvaluationReport
{
    public RegexEvaluationReport(
        IEnumerable<RuleEvaluationResult> ruleResults,
        BitArray coveredSamples,
        TimeSpan totalDuration)
    {
        RuleResults = new ReadOnlyCollection<RuleEvaluationResult>((ruleResults ?? throw new ArgumentNullException(nameof(ruleResults))).ToList());
        CoveredSamples = coveredSamples ?? throw new ArgumentNullException(nameof(coveredSamples));
        TotalDuration = totalDuration;
        Metrics = RegexEvaluationMetrics.BuildSummary(RuleResults.Select(r => r.Coverage), RuleResults.SelectMany(r => r.SampleResults), totalDuration);
    }

    public IReadOnlyList<RuleEvaluationResult> RuleResults { get; }

    public BitArray CoveredSamples { get; }

    public TimeSpan TotalDuration { get; }

    public RegexEvaluationMetrics Metrics { get; }

    public IReadOnlyList<int> GetUnmatchedSampleIndices()
    {
        return Enumerable.Range(0, CoveredSamples.Length)
            .Where(i => !CoveredSamples[i])
            .ToList();
    }
}
