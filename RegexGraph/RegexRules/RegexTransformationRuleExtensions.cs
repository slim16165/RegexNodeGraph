using System;

namespace RegexNodeGraph.RegexRules;

public static class RegexTransformationRuleExtensions
{
    public static string GetDisplayName(this RegexTransformationRule rule, int maxLength = 60)
    {
        if (rule is null) throw new ArgumentNullException(nameof(rule));
        if (maxLength <= 0) return string.Empty;

        string source = string.IsNullOrWhiteSpace(rule.Description) ? rule.From : rule.Description!;
        if (string.IsNullOrEmpty(source)) return string.Empty;

        if (source.Length <= maxLength) return source;

        int visibleLength = Math.Max(0, maxLength - 1);
        if (visibleLength == 0)
        {
            return "\u2026";
        }

        return source[..visibleLength] + "\u2026";
    }
}
