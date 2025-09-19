using System;
using RegexNodeGraph.RegexRules;

namespace RegexNodeGraph.Evaluation;

public sealed class SampleMatchResult
{
    public SampleMatchResult(
        int sampleIndex,
        RegexSample sample,
        RegexTransformationRule rule,
        bool isMatch,
        string output,
        TimeSpan elapsed,
        Exception? error = null)
    {
        SampleIndex = sampleIndex;
        Sample = sample ?? throw new ArgumentNullException(nameof(sample));
        Rule = rule ?? throw new ArgumentNullException(nameof(rule));
        IsMatch = isMatch;
        Output = output ?? string.Empty;
        Elapsed = elapsed;
        Error = error;
    }

    public int SampleIndex { get; }
    public RegexSample Sample { get; }
    public RegexTransformationRule Rule { get; }
    public bool IsMatch { get; }
    public string Output { get; }
    public TimeSpan Elapsed { get; }
    public Exception? Error { get; }
}
