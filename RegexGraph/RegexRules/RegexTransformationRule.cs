using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RegexNodeGraph.Graph.GraphCore;
using RegexNodeGraph.Model;

namespace RegexNodeGraph.RegexRules
{
    public class RegexTransformationRule : ITransformationRule
    {
        // Definizione della regex
        public Regex RegexFrom { get; set; }
        public string From => RegexFrom.ToString();

        // Replacement e metadata
        public string To { get; set; }
        public string Description { get; set; }
        public List<string> Categories { get; set; } = new List<string>();

        // Opzioni di configurazione
        private ConfigOptions _configOptions;
        public ConfigOptions ConfigOptions
        {
            get => _configOptions;
            set => _configOptions = value;
        }

        // Statistiche di esecuzione
        public int Count { get; private set; }
        public long TotalTime { get; private set; }
        public List<RegexDebugData> DebugData { get; } = new();

        // Comportamenti su match
        public bool EsciInCasoDiMatch => (ConfigOptions & ConfigOptions.EsciInCasoDiMatch) == ConfigOptions.EsciInCasoDiMatch;
        public bool ShouldRetryFromOrig => (ConfigOptions & ConfigOptions.IgnoraRetry) != ConfigOptions.IgnoraRetry;

        // Costruttori
        public RegexTransformationRule(string from, string to, ConfigOptions configOptions)
        {
            RegexFrom = new Regex(from, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            To = to;
            ConfigOptions = configOptions;
        }

        public RegexTransformationRule(string from, string to, string description, List<string> categories, ConfigOptions configOptions)
            : this(from, to, configOptions)
        {
            Description = description;
            Categories = categories ?? new List<string>();
        }

        public RegexTransformationRule((string from, string to) regex, string description = "")
            : this(regex.from, regex.to, ConfigOptions.NonUscireInCasoDiMatch)
        {
            Description = description;
        }

        // Implementazione di ITransformationRule

        /// <summary>
        /// Applica la regola a un oggetto Description e restituisce DebugData e la Description modificata.
        /// </summary>
        public (RegexDebugData res, Description description) Apply(Description description)
        {
            // Si appoggia all'extension method in RegexHelper
            return this.ApplyReplacement(description);
        }

        /// <summary>
        /// Applica la regola a una stringa di input e restituisce il DebugData.
        /// </summary>
        public RegexDebugData Apply(string input)
        {
            return this.ApplyReplacement(input);
        }

        /// <summary>
        /// Simula l'applicazione senza modificare la Description.
        /// </summary>
        public RegexDebugData Simulate(string input)
        {
            return this.SimulateApplication(input);
        }

        /// <summary>
        /// Incrementa il contatore di applicazioni della regola e registra il tempo impiegato.
        /// </summary>
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

        /// <summary>
        /// Solo incremento del tempo totale senza contare una trasformazione.
        /// </summary>
        public void IncrementOnlyCounter(long timeTaken)
        {
            TotalTime += timeTaken;
        }

        public override string ToString()
        {
            return $"From: {RegexFrom}, To: {To}, Categories: [{string.Join(", ", Categories)}], Count: {Count}, TotalTime: {TotalTime}ms";
        }
    }
}