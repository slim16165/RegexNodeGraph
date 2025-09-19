using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace RegexNodeGraph.Evaluation;

/// <summary>
/// Represents an input sample that must be evaluated against a collection of regex rules.
/// </summary>
public sealed class RegexSample
{
    public RegexSample(string text, string? identifier = null, IReadOnlyDictionary<string, object?>? metadata = null)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        Identifier = identifier;
        Metadata = metadata switch
        {
            null => EmptyMetadata,
            ReadOnlyDictionary<string, object?> readOnly => readOnly,
            _ => new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>(metadata))
        };
    }

    /// <summary>
    /// Gets the identifier of the sample (if any). This can be used to correlate results with domain entities.
    /// </summary>
    public string? Identifier { get; }

    /// <summary>
    /// Gets the text that must be evaluated.
    /// </summary>
    public string Text { get; }

    /// <summary>
    /// Gets a metadata bag that can carry additional information about the sample.
    /// </summary>
    public IReadOnlyDictionary<string, object?> Metadata { get; }

    public void Deconstruct(out string text, out string? identifier)
    {
        text = Text;
        identifier = Identifier;
    }

    public override string ToString()
    {
        return string.IsNullOrEmpty(Identifier) ? Text : $"{Identifier}: {Text}";
    }

    private static readonly IReadOnlyDictionary<string, object?> EmptyMetadata =
        new ReadOnlyDictionary<string, object?>(new Dictionary<string, object?>());
}
