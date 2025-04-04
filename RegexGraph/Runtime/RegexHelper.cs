﻿using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using RegexNodeGraph.Runtime.Graph.Model;

namespace RegexNodeGraph.Runtime;

public static class RegexHelper
{
    /// <summary>
    /// Crea una regex con word boundaries.
    /// </summary>
    public static string WordBoundary(string pattern) => $@"\b{pattern}\b";

    /// <summary>
    /// Crea una regex che permette qualsiasi carattere prima e dopo il pattern.
    /// </summary>
    public static string PartialMatch(string pattern) => $@".*{pattern}.*";

    /// <summary>
    /// Crea una regex per una lista di termini alternati.
    /// </summary>
    public static string Alternation(params string[] terms) => $"({string.Join("|", terms)})";

    /// <summary>
    /// Crea una regex per un pattern specifico di date.
    /// </summary>
    public static string DatePattern() => @"(\b|del )\d{2}([./:])\d{2}(\2\d{2,4})?\b";

    /// <summary>
    /// Crea una regex per un IBAN generico.
    /// </summary>
    public static string IbanPattern() => @"\b[A-Z]{2}\d{4}[A-Z]{3}\d\b";

    /// <summary>
    /// Crea una regex per un codice bancario generico.
    /// </summary>
    public static string BankCodePattern() => @"\b[A-Z]{0,4}[0-9:,_/-]{3,}[A-Z]{0,6}\b";

    /// <summary>
    /// Crea una regex per riconoscere diverse valute.
    /// </summary>
    public static string CurrencyPattern(params string[] currencies) => $@"\b({string.Join("|", currencies)})\b";

    /// <summary>
    /// Crea una regex con un placeholder dinamico.
    /// </summary>
    public static string Placeholder(string placeholder) => $@"\{{{placeholder}}}";

    /// <summary>
    /// Crea una regex per riconoscere spazi multipli.
    /// </summary>
    public static string MultipleSpaces() => @"\s+";

    /// <summary>
    /// Verifica se il pattern contiene un'alternanza.
    /// </summary>
    public static bool IsAlternation(string pattern) => pattern.Contains("|");

    /// <summary>
    /// Estrae i termini di un'alternanza da un pattern.
    /// </summary>
    public static string[] ExtractAlternationTerms(string pattern)
    {
        var match = Regex.Match(pattern, @"\(([^)]+)\)");
        return match.Success ? match.Groups[1].Value.Split('|') : [];
    }

    public static (RegexDebugData res, Description description) ApplyReplacement(this RegexDescription rule, Description description)
    {
        // Utilizziamo CurrentDescription dalla classe
        string input = description.CurrentDescription ?? string.Empty;
        string output = input;
        // Reset del flag HasChangedNow prima dell'applicazione della regola
        description.HasChangedNow = false;

        Stopwatch stopwatch = Stopwatch.StartNew();

        bool isMatch = rule.RegexFrom.IsMatch(input);
        if (isMatch)
        {
            output = rule.RegexFrom.Replace(input, rule.To);
        }

        Console.WriteLine($"Applying regex: {rule.RegexFrom} with IgnoreCase: {rule.RegexFrom.Options.HasFlag(RegexOptions.IgnoreCase)}; Input: {input}; Match found: {isMatch}; Output after replacement: {output}");


        stopwatch.Stop();

        var res = new RegexDebugData(input, rule, output, isMatch);

        if (isMatch && output != input)
        {
            // Incrementa Count solo se la stringa risultante è diversa dall'input originale
            rule.IncrementCount(res, stopwatch.ElapsedMilliseconds);

            // Aggiorna la descrizione direttamente
            description.UpdateDescription(output);

            // Determiniamo se dobbiamo interrompere in base alle opzioni di configurazione della regola
            // Se c'è stato un match (output != input) e la regex prevede di uscire in caso di match

            description.IsCategorized = rule.EsciInCasoDiMatch; //se c'è stata una modifica e la regola è configurata per gestire la cosa


            if (description.IsCategorized && rule.EsciInCasoDiMatch)
            {
                if(output.Length > 30)
                    Debugger.Break();
                description.Category = output;
            }
        }
        else
        {
            rule.IncrementOnlyCounter(stopwatch.ElapsedMilliseconds);
        }

        if (res.Output == null)
            throw new InvalidOperationException("Result output is null");

        return (res, description);
    }


    public static RegexDebugData ApplyReplacement(this RegexDescription rule, string input)
    {
        input ??= "";
        string output = input;

        Stopwatch stopwatch = Stopwatch.StartNew();

        bool isMatch = rule.RegexFrom.IsMatch(input);
        if (isMatch)
            output = rule.RegexFrom.Replace(input, rule.To);

        stopwatch.Stop();

        var res = new RegexDebugData(input, rule, output, isMatch);

        if (isMatch /*output != input*/)
        {
            // Incrementa Count solo se la stringa risultante è diversa dall'input originale

            rule.IncrementCount(res, stopwatch.ElapsedMilliseconds);
        }
        else
        {
            rule.IncrementOnlyCounter(stopwatch.ElapsedMilliseconds);
        }

        if (res.Output == null) throw new InvalidOperationException("Result output is null");

        return res;
    }

    public static RegexDebugData SimulateApplication(this RegexDescription r, string input)
    {
        input ??= "";
        string output = input;

        bool isMatch = r.RegexFrom.IsMatch(input);
        if (isMatch)
            output = r.RegexFrom.Replace(input, r.To);

        var res = new RegexDebugData(input, r, output, isMatch);

        return res;
    }
}