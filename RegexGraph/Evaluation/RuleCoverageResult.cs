using System;
using System.Collections.Generic;
using RegexNodeGraph.RegexRules;

namespace RegexNodeGraph.Evaluation;

public sealed class RuleCoverageResult
{
    public RuleCoverageResult(
        int ruleIndex,
        RegexTransformationRule rule,
        int matchCount,
        int totalSamples,
        TimeSpan elapsed,
        double matchThroughput,
        double evalThroughput,
        IReadOnlyList<int> matchedSampleIndices,
        Exception? error = null)
    {
        RuleIndex = ruleIndex;
        Rule = rule ?? throw new ArgumentNullException(nameof(rule));
        MatchCount = matchCount;
        TotalSamples = totalSamples;
        Elapsed = elapsed;
        MatchThroughput = matchThroughput;
        EvalThroughput = evalThroughput;
        MatchedSampleIndices = matchedSampleIndices ?? Array.Empty<int>();
        Error = error;
    }

    public int RuleIndex { get; }
    public RegexTransformationRule Rule { get; }
    public int MatchCount { get; }
    public int TotalSamples { get; }
    public TimeSpan Elapsed { get; }
    public double MatchThroughput { get; }
    public double EvalThroughput { get; }
    public IReadOnlyList<int> MatchedSampleIndices { get; }
    public Exception? Error { get; }

    public bool HasError => Error is not null;
}
