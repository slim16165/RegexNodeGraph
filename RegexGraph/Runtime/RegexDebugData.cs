using System.Collections.Generic;

namespace RegexNodeGraph.Runtime;


/// <summary>
/// Represents the details of a regex replacement operation.
/// </summary>
public class RegexDebugData
{
    public RegexDebugData()
    {
    }

    public RegexDebugData(string input, RegexDescription regex, string output, bool isMatch)
    {
        Input = input;
        Regex = regex;
        Output = output;
        IsMatch = isMatch;
    }

    public string Input { get; set; }
    public string Output { get; set; }

    public RegexDescription Regex { get; set; }

    public bool IsMatch { set; get; }
    public string Match { get; set; }
    public int Position { get; set; }

    /// <summary>
    /// Total number of replacements made using the regex pattern
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Number of descriptions transformed by this regex application
    /// </summary>
    ///
    /// Count (in RegexDescription):
    /// Rappresenta il numero di volte in cui la RegexDescription è stata applicata con successo(ha trovato un match) a una stringa di input.
    ///     Viene incrementato una volta per ogni chiamata di ApplyReplacement che risulta in un match, indipendentemente dal numero di sostituzioni effettuate all'interno della stringa.
    ///     TransformationCount(in TransformationEdge) :
    /// Rappresenta il numero di descrizioni che sono state effettivamente trasformate da un nodo sorgente a un nodo destinazione attraverso una specifica RegexDescription.
    ///     Viene calcolato contando quante descrizioni nel nodo sorgente sono state modificate dall'applicazione della RegexDescription.
    /// </summary>
    public int TransformationCount { get; set; }

    /// <summary>
    /// List of descriptions that were transformed
    /// </summary>
    public List<string> TransformedDescriptions { get; set; } = new List<string>();

    ///// <summary>
    ///// Number of distinct replacements made using the regex pattern
    ///// </summary>
    //public int CountDistinct { get; set; }
}