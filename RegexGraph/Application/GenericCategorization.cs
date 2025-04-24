using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using RegexNodeGraph.Graph.GraphCore;
using RegexNodeGraph.Graph.Processing;
using RegexNodeGraph.Model;
using RegexNodeGraph.RegexRules;

namespace RegexNodeGraph.Application;

public class GenericCategorization
{
    public RegexRuleBuilder Rules { get; set; }
    private bool debugProcessSingleDescription;

    public GenericCategorization(RegexRuleBuilder rules, bool debugProcessSingleDescription)
    {
        Rules = rules;
        this.debugProcessSingleDescription = debugProcessSingleDescription;
    }

    public async Task<(List<Description> transactionDesc, TransformationGraph graph)> CategorizeDescriptions(List<string> descriptions)
    {
        // Converte le stringhe di descrizione in oggetti Description
        List<Description> transactionDescriptions = descriptions
            .Select(desc => new Description(desc))
            .ToList();

        // Crea un nuovo grafo di trasformazione
        var graph = new TransformationGraph();

        // Costruisce la lista delle regole regex dalla proprietà Rules
        List<RegexTransformationRule> regexDescriptions = Rules.Build();

        // TODO: Flag di debug: se impostato a true, ogni descrizione viene processata singolarmente
        // Lascia che sia il flag passato dal chiamante a decidere
        debugProcessSingleDescription = false;

        await Task.Run(() =>
        {
            if (!debugProcessSingleDescription)
            {
                //// Elaborazione batch: costruisce il grafo per tutte le descrizioni insieme
                //graph.BuildGraph(transactionDescriptions, Rules.Build());

                Parallel.ForEach(transactionDescriptions, desc =>
                {
                    // applichi la pipeline di regex (senza usare il grafo)
                    foreach (var rule in regexDescriptions)
                        rule.ApplyReplacement(desc);
                });

                // una sola BuildGraph alla fine
                graph.BuildGraph(transactionDescriptions, regexDescriptions);
            }
            else
            {
                // Elaborazione singola: processa ogni descrizione separatamente
                for (int i = 0; i < transactionDescriptions.Count; i++)
                {
                    // Crea una lista contenente solo la descrizione corrente
                    List<Description> singleDescList = [transactionDescriptions[i]];

                    // Costruisce il grafo per questa singola descrizione
                    graph.BuildGraph(singleDescList, regexDescriptions);

                    // Se la descrizione trasformata è "larga" (più di 40 caratteri),
                    // genera e stampa un report di debug
                    if (singleDescList[0].CurrentDescription.Length > 40)
                    {
                        string debugReport = GenerateDebugReportForTransaction(singleDescList[0], graph);
                        Console.WriteLine(debugReport);
                        // Debugger.Break(); // Decommenta se desideri interrompere l'esecuzione per il debug
                    }
                }
            }
        });

        return (transactionDescriptions, graph);
    }

    public static string GenerateDebugReportForTransaction(Description transaction, TransformationGraph graph)
    {
        // Trova il DetailedTransactionNode associato alla transazione
        var detailedNode = graph.Nodes
            .OfType<DetailedTransactionNode>()
            .FirstOrDefault(n => n.Description == transaction);

        if (detailedNode == null)
        {
            return $"[DEBUG] No detailed node found for transaction: {transaction.OriginalDescription}";
        }

        // Imposta la modalità verbose (potrebbe essere reso un parametro in futuro)
        bool verbose = true;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== DEBUG REPORT FOR TRANSACTION ===");
        sb.AppendLine($"[Node ID: {detailedNode.Id}]");
        sb.AppendLine($"Original Description: {transaction.OriginalDescription}");
        sb.AppendLine($"Final Description: {transaction.CurrentDescription}");
        sb.AppendLine($"Is Categorized: {transaction.IsCategorized}");
        if (transaction.IsCategorized && !string.IsNullOrEmpty(transaction.Category))
        {
            sb.AppendLine($"Assigned Category: {transaction.Category}");
        }
        sb.AppendLine();

        // --- Sezione: Trasformazioni a livello di dettaglio ---
		// Ora recuperiamo le trasformazioni. Possiamo traversare il grafo partendo dal detailedNode
        // e guardando tutti gli archi di trasformazione usati.
        // Nel caso di questa implementazione, diamo per scontato che ci sia un modo lineare di risalire alle regole.
        // Se ci sono molte trasformazioni, potremmo fare un BFS/DFS per ordinare i passaggi.

        // Gli archi di dettaglio (DetailTransformationEdge) contengono i DebugData delle singole regole applicate
        var detailEdges = graph.Edges
            .OfType<DetailTransformationEdge>()
            .Where(e => e.SourceNode == detailedNode || e.TargetNode == detailedNode)
            .ToList();

        if (!detailEdges.Any())
        {
            // Nessuna trasformazione a livello di dettaglio. Possibile che la transazione non sia stata modificata
            sb.AppendLine("No detail transformations found for this transaction.");
        }
        else
        {
            sb.AppendLine("Detail Transformations:");
            int step = 1;
            foreach (var edge in detailEdges)
            {
                sb.AppendLine($"  [Step {step++}]");
                sb.AppendLine($"    Regex From: {edge.RegexRule.From}");
                sb.AppendLine($"    Regex To: {edge.RegexRule.To}");
                sb.AppendLine($"    Rule Description: {edge.RegexRule.Description}");
                sb.AppendLine($"    Categories: {string.Join(", ", edge.RegexRule.Categories)}");
                sb.AppendLine($"    Input: {edge.DebugData.Input}");
                sb.AppendLine($"    Output: {edge.DebugData.Output}");
                sb.AppendLine($"    Matched: {edge.DebugData.IsMatch}");
                // Se in futuro si aggiunge la misurazione del tempo, ad esempio:
                // sb.AppendLine($"    Time Taken: {edge.DebugData.TimeTaken} ms");
                sb.AppendLine($"    TransformationCount (Rule): {((RegexTransformationRule)edge.DebugData.TransformationRule).Count}");
                sb.AppendLine();
            }
        }
        sb.AppendLine();

        // --- Sezione: Trasformazioni Aggregate ---
		// Se vogliamo tracciare anche le trasformazioni aggregate (AggregatedTransactionsNode)
        // Possiamo guardare i TransformationEdge
        var aggregatedEdges = graph.Edges
            .OfType<TransformationEdge>()
            .Where(e => e.SourceNode is AggregatedTransactionsNode && e.TargetNode is AggregatedTransactionsNode)
            .ToList();

        if (aggregatedEdges.Any())
        {
            sb.AppendLine("Aggregated Transformations:");
            int aggStep = 1;
            foreach (var aggEdge in aggregatedEdges)
            {
                sb.AppendLine($"  [Aggregated Step {aggStep++}]");
                sb.AppendLine($"    Regex From: {aggEdge.RegexRule.From}");
                sb.AppendLine($"    Regex To: {aggEdge.RegexRule.To}");
                sb.AppendLine($"    Rule Description: {aggEdge.RegexRule.Description}");
                sb.AppendLine($"    Categories: {string.Join(", ", aggEdge.RegexRule.Categories)}");
                sb.AppendLine($"    TransformationCount (edge): {aggEdge.TransformationCount}");
                if (verbose && aggEdge.DebugData.Any())
                {
                    sb.AppendLine("    Debug Data:");
                    foreach (var d in aggEdge.DebugData)
                    {
                        sb.AppendLine($"      Input: {d.Input}");
                        sb.AppendLine($"      Output: {d.Output}");
                        sb.AppendLine($"      Matched: {d.IsMatch}");
                        sb.AppendLine();
                    }
                }
                sb.AppendLine();
            }
        }
        else
        {
            sb.AppendLine("No aggregated transformations found for this transaction.");
        }

        sb.AppendLine("=== END OF DEBUG REPORT ===");
        return sb.ToString();
    }

    public static void GenerateAndLogCypherQueries(TransformationGraph graph, List<Description> transactionDescriptions)
    {
        // Generazione delle query Cypher
        CypherQueryGenerator queryGenerator = new CypherQueryGenerator(graph);
        string cypherQueries = queryGenerator.GenerateCypherQueries();

        // Stampa o salvataggio delle query
        Console.WriteLine("Cypher Queries:");
        Console.WriteLine(cypherQueries);

        List<Description> filtered = transactionDescriptions.Where(s => s.CurrentDescription.Length > 20).ToList();


        string s = JsonSerializer.Serialize(filtered, new JsonSerializerOptions { WriteIndented = true });
    }
}