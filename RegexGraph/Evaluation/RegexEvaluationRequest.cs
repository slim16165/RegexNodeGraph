using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using RegexNodeGraph.RegexRules;

namespace RegexNodeGraph.Evaluation;

/// <summary>
/// Encapsulates the data required to evaluate one or more <see cref="RegexTransformationRule"/> objects over a set of samples.
/// </summary>
public sealed class RegexEvaluationRequest
{
    private readonly Dictionary<RegexTransformationRule, int> _ruleIndexMap;

    public RegexEvaluationRequest(
        IEnumerable<RegexTransformationRule> rules,
        IEnumerable<RegexSample> samples,
        RegexOptions? optionsOverride = null,
        TimeSpan? matchTimeout = null,
        bool recompileExpressions = true,
        bool useRegexCache = false,
        int degreeOfParallelism = 1)
    {
        if (rules is null) throw new ArgumentNullException(nameof(rules));
        if (samples is null) throw new ArgumentNullException(nameof(samples));

        Rules = rules.ToList();
        Samples = samples.ToList();
        if (Rules.Count == 0) throw new ArgumentException("At least one rule must be provided", nameof(rules));
        if (Samples.Count == 0) throw new ArgumentException("At least one sample must be provided", nameof(samples));

        if (degreeOfParallelism <= 0)
            throw new ArgumentOutOfRangeException(nameof(degreeOfParallelism), "Degree of parallelism must be greater than zero.");

        OptionsOverride = optionsOverride;
        MatchTimeout = matchTimeout;
        RecompileExpressions = recompileExpressions;
        UseRegexCache = useRegexCache;
        DegreeOfParallelism = degreeOfParallelism;

        _ruleIndexMap = new Dictionary<RegexTransformationRule, int>(Rules.Count, RuleReferenceComparer.Instance);
        for (int i = 0; i < Rules.Count; i++)
        {
            _ruleIndexMap[Rules[i]] = i;
        }
    }

    public IReadOnlyList<RegexTransformationRule> Rules { get; }

    public IReadOnlyList<RegexSample> Samples { get; }

    /// <summary>
    /// Allows overriding the options used when compiling the regex. When null the rule options are used.
    /// </summary>
    public RegexOptions? OptionsOverride { get; }

    /// <summary>
    /// Allows overriding the timeout used during regex execution.
    /// </summary>
    public TimeSpan? MatchTimeout { get; }

    /// <summary>
    /// When true the engine will recompile the expressions using the provided options.
    /// </summary>
    public bool RecompileExpressions { get; }

    /// <summary>
    /// When true the engine will reuse compiled regex instances for identical pattern/options pairs.
    /// </summary>
    public bool UseRegexCache { get; }

    /// <summary>
    /// Controls the level of parallelism used when evaluating the rules. Use 1 to keep sequential execution.
    /// </summary>
    public int DegreeOfParallelism { get; }

    internal int GetRuleIndex(RegexTransformationRule rule)
    {
        if (rule is null) throw new ArgumentNullException(nameof(rule));
        if (_ruleIndexMap.TryGetValue(rule, out var index))
            return index;

        // Fallback to structural search for compatibility with externally provided clones.
        for (int i = 0; i < Rules.Count; i++)
        {
            if (ReferenceEquals(Rules[i], rule) || Rules[i].Equals(rule))
            {
                _ruleIndexMap[rule] = i;
                return i;
            }
        }

        throw new ArgumentException("The provided rule is not part of the evaluation request", nameof(rule));
    }

    public static RegexOptions DefaultRegexOptions { get; } = RegexOptions.CultureInvariant | RegexOptions.Singleline;

    public static RegexEvaluationRequest FromTexts(
        IEnumerable<RegexTransformationRule> rules,
        IEnumerable<string> texts,
        RegexOptions? optionsOverride = null,
        TimeSpan? matchTimeout = null,
        bool recompileExpressions = true,
        bool useRegexCache = false,
        int degreeOfParallelism = 1)
    {
        if (rules is null) throw new ArgumentNullException(nameof(rules));
        if (texts is null) throw new ArgumentNullException(nameof(texts));

        var sampleList = texts.Select(text => new RegexSample(text)).ToList();
        return new RegexEvaluationRequest(rules, sampleList, optionsOverride, matchTimeout, recompileExpressions, useRegexCache, degreeOfParallelism);
    }

    private sealed class RuleReferenceComparer : IEqualityComparer<RegexTransformationRule>
    {
        public static RuleReferenceComparer Instance { get; } = new();

        public bool Equals(RegexTransformationRule? x, RegexTransformationRule? y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(RegexTransformationRule obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}
