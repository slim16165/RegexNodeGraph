using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace RegexNodeGraph.Evaluation;

public sealed class RuleEvaluationResult
{
    public RuleEvaluationResult(RuleCoverageResult coverage, IEnumerable<SampleMatchResult> sampleResults)
    {
        Coverage = coverage ?? throw new ArgumentNullException(nameof(coverage));
        SampleResults = new ReadOnlyCollection<SampleMatchResult>((sampleResults ?? throw new ArgumentNullException(nameof(sampleResults))).ToList());
    }

    public RuleCoverageResult Coverage { get; }

    public IReadOnlyList<SampleMatchResult> SampleResults { get; }
}
