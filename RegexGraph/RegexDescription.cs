using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RegexNodeGraph.Runtime;

namespace RegexNodeGraph;

public class RegexDescription
{
    public Regex RegexFrom { get; set; }

    public List<string> Categories { get; set; } = new List<string>();
    
    //public int Id { get; set; }

    //public int ApplyAfterId { get; set; }

    /// <summary>
    /// The regex description
    /// </summary>
    public string Description { get; set; }

    public string To { get; set; }
    private ConfigOptions _configOptions;

    public ConfigOptions ConfigOptions
    {
        get => _configOptions;
        set
        {
            // Al set, potremmo controllare conflitti se necessario.
            _configOptions = value;
        }
    }

    public string From => RegexFrom.ToString();

    public int Count { get; set; }
    public long TotalTime { get; set; }
    public List<RegexDebugData> DebugData { get; set; } = new();

    public bool EsciInCasoDiMatch => (this.ConfigOptions & ConfigOptions.EsciInCasoDiMatch) == ConfigOptions.EsciInCasoDiMatch;

    public bool ShouldRetryFromOrig => (this.ConfigOptions & ConfigOptions.IgnoraRetry) != ConfigOptions.IgnoraRetry;

    public RegexDescription(string from, string to, ConfigOptions configOptions)
    {
        RegexFrom = new Regex(from, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        To = to;
        ConfigOptions = configOptions;
    }

    public RegexDescription(string from, string to, string description, List<string> categories, ConfigOptions configOptions)
    {
        RegexFrom = new Regex(from, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        To = to;
        Description = description;
        Categories = categories;
        ConfigOptions = configOptions;
    }

    public RegexDescription((string from, string to) regex, string description = "")
    {
        RegexFrom = new Regex(regex.from, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        Description = description;
        To = regex.to;
    }

    public void IncrementCount(RegexDebugData res, long timeTaken)
    {
        Count++;
        TotalTime += timeTaken;
        DebugData.Add(res);

        // Aggiungi informazioni di debug su categorie e descrizione
        Console.WriteLine($"[DEBUG] Regola applicata: {Description}");
        Console.WriteLine($"[DEBUG] Categorie: {string.Join(", ", Categories)}");
        Console.WriteLine($"[DEBUG] Tempo impiegato: {timeTaken} ms");
        Console.WriteLine($"[DEBUG] Numero di trasformazioni: {Count}");
    }
    

    public void IncrementOnlyCounter(long timeTaken)
    {
        TotalTime += timeTaken;
    }

    public override string ToString()
    {
        return $"From: {RegexFrom}, To: {To}, Categories: [{string.Join(", ", Categories)}], Count: {Count}, TotalTime: {TotalTime}ms";
    }
}