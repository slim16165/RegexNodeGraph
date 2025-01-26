using System;
using System.Collections;
using System.Collections.Generic;

namespace RegexNodeGraph;

public class RegexRuleBuilder : IEnumerable<RegexDescription>
{
    private readonly List<RegexDescription> _rules = new List<RegexDescription>();

    /// <summary>
    /// Aggiunge una nuova regola semplice.
    /// </summary>
    public RegexRuleBuilder Add(string from, string to,
        ConfigOptions configOptions = ConfigOptions.NonUscireInCasoDiMatch)
    {
        _rules.Add(new RegexDescription(from, to, configOptions));
        return this;
    }

    /// <summary>
    /// Aggiunge una regola con descrizione e categorie.
    /// </summary>
    public RegexRuleBuilder Add(string from, string to, string description,
        List<string> categories = null, ConfigOptions configOptions = ConfigOptions.NonUscireInCasoDiMatch)
    {
        var regexDescription = new RegexDescription(from, to, description, categories ?? new List<string>(), configOptions);
        _rules.Add(regexDescription);
        return this;
    }

    /// <summary>
    /// Aggiunge una categoria a tutte le regole attuali.
    /// </summary>
    public RegexRuleBuilder Categorize(string category)
    {
        foreach (var rule in _rules)
        {
            if (!rule.Categories.Contains(category))
            {
                rule.Categories.Add(category);
            }
        }
        return this;
    }

    /// <summary>
    /// Unisce le opzioni con quelle esistenti. Se si verifica un conflitto, mostra un warning.
    /// La logica del conflitto può essere customizzata: in questo esempio, se si cerca di "andare contro"
    /// un'opzione che prevede l'uscita su match, si genera un warning.
    /// </summary>
    public RegexRuleBuilder WithOptions(ConfigOptions options)
    {
        foreach (var rule in _rules)
        {
            var oldOptions = rule.ConfigOptions;
            var newOptions = MergeOptions(oldOptions, options, out bool conflict);

            if (conflict)
            {
                Console.Error.WriteLine(
                    $"[WARNING] Conflict merging options for rule '{rule.RegexFrom}': old = {oldOptions}, requested = {options}, merged = {newOptions}");
            }

            rule.ConfigOptions = newOptions;
        }
        return this;
    }

    /// <summary>
    /// Tenta di unire le opzioni. Logica:
    /// - Se la nuova opzione include EsciInCasoDiMatch e la vecchia include NonUscireInCasoDiMatch,
    ///   prevale EsciInCasoDiMatch.
    /// - Se la nuova opzione è in conflitto con la precedente (es: prima EsciInCasoDiMatch e ora NonUscireInCasoDiMatch),
    ///   mantieni quella più “forte” (EsciInCasoDiMatch) e segnala conflitto.
    /// - IgnoraRetry può essere sempre unita senza conflitti.
    /// </summary>
    private static ConfigOptions MergeOptions(ConfigOptions oldOptions, ConfigOptions newOptions, out bool conflict)
    {
        conflict = false;

        // Caso semplice: se nessun conflitto logico, semplicemente esegue un OR bitwise
        // e poi risolve i conflitti di combinazione.
        var combined = oldOptions | newOptions;

        // Risolvi conflitti
        // Ad esempio: NonUscireInCasoDiMatch e EsciInCasoDiMatch insieme non hanno molto senso.
        // Prevale EsciInCasoDiMatch.
        bool oldHadEsci = (oldOptions & ConfigOptions.EsciInCasoDiMatch) == ConfigOptions.EsciInCasoDiMatch;
        bool newHasEsci = (newOptions & ConfigOptions.EsciInCasoDiMatch) == ConfigOptions.EsciInCasoDiMatch;
        bool oldHadNonUscire = (oldOptions & ConfigOptions.NonUscireInCasoDiMatch) == ConfigOptions.NonUscireInCasoDiMatch;
        bool newHasNonUscire = (newOptions & ConfigOptions.NonUscireInCasoDiMatch) == ConfigOptions.NonUscireInCasoDiMatch;

        // Se prima era stabilito EsciInCasoDiMatch e adesso si chiede NonUscireInCasoDiMatch
        // Questo è un conflitto. Manteniamo EsciInCasoDiMatch.
        if (oldHadEsci && newHasNonUscire)
        {
            combined &= ~ConfigOptions.NonUscireInCasoDiMatch; // rimuovi NonUscireInCasoDiMatch
            conflict = true;
        }

        // Se prima era NonUscire e ora si chiede EsciInCasoDiMatch, non è un vero conflitto,
        // semplicemente innalziamo lo "stato" a EsciInCasoDiMatch.
        // Nessun warning necessario in questo caso. Semplicemente EsciInCasoDiMatch è più forte.

        // IgnoraRetry può semplicemente essere aggiunto senza conflitti.

        return combined;
    }

    public RegexRuleBuilder IgnoreRetry()
    {
        return WithOptions(ConfigOptions.IgnoraRetry);
    }

    public RegexRuleBuilder ExitOnMatch()
    {
        return WithOptions(ConfigOptions.EsciInCasoDiMatch);
    }

    public List<RegexDescription> Build()
    {
        return _rules;
    }

    public IEnumerator<RegexDescription> GetEnumerator()
    {
        return _rules.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void AddRange(List<RegexDescription> regexDescriptions)
    {
        _rules.AddRange(regexDescriptions);
    }

    public static RegexRuleBuilder Combine(params RegexRuleBuilder[] builders)
    {
        var combinedBuilder = new RegexRuleBuilder();
        foreach (var builder in builders)
        {
            combinedBuilder._rules.AddRange(builder._rules);
        }
        return combinedBuilder;
    }
}