using System;
using RegexNodeGraph.RegexRules;

namespace RegexNodeGraph.Evaluation;

public abstract class RegexEvaluationException : Exception
{
    protected RegexEvaluationException(string message, RegexTransformationRule rule, Exception? inner = null)
        : base(message, inner)
    {
        Rule = rule;
    }

    public RegexTransformationRule Rule { get; }
}

public sealed class RegexCompilationException : RegexEvaluationException
{
    public RegexCompilationException(string message, RegexTransformationRule rule, Exception inner)
        : base(message, rule, inner)
    {
    }
}

public sealed class RegexExecutionException : RegexEvaluationException
{
    public RegexExecutionException(string message, RegexTransformationRule rule, Exception inner)
        : base(message, rule, inner)
    {
    }
}
