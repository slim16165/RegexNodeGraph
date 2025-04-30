using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RegexNodeGraph.Model;

namespace RegexNodeGraph.RegexRules
{
    public class RegexTransformationRule : TransformationRuleBase, IRegexRuleMetadata
    {
        // Definizione della regex
        public Regex RegexFrom { get; set; }
        public string From => RegexFrom.ToString();

        // Replacement e metadata
        public string To { get; set; }
        public string Description { get; set; }
        public string ToDebugString()
        {
            return $"""
                        Regex From: {From}
                        Regex To: {To}
                        Rule Description: {Description}
                        Categories: {string.Join(", ", Categories)}
                    """;
        }

        public List<string> Categories { get; set; } = new List<string>();

        // Statistiche di esecuzione
        public int Count { get; private set; }
        public long TotalTime { get; private set; }
        public List<TransformationDebugData> DebugData { get; } = new();
        
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
        public override (TransformationDebugData res, Description description) Apply(Description description)
        {
            // Si appoggia all'extension method in RegexHelper
            return this.ApplyReplacement(description);
        }

        /// <summary>
        /// Applica la regola a una stringa di input e restituisce il DebugData.
        /// </summary>
        public override TransformationDebugData Apply(string input)
        {
            return this.ApplyReplacement(input);
        }

        /// <summary>
        /// Simula l'applicazione senza modificare la Description.
        /// </summary>
        public override TransformationDebugData Simulate(string input)
        {
            return this.SimulateApplication(input);
        }

        /// <summary>
        /// Incrementa il contatore di applicazioni della regola e registra il tempo impiegato.
        /// </summary>
        public void IncrementCount(TransformationDebugData res, long timeTaken)
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