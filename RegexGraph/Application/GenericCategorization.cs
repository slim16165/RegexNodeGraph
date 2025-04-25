using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using RegexNodeGraph.Engine;                    // ← motore puro
using RegexNodeGraph.Graph.GraphCore;
using RegexNodeGraph.Graph.Processing;          // ← builder + graph DTO
using RegexNodeGraph.Model;
using RegexNodeGraph.RegexRules;

namespace RegexNodeGraph.Application;

public partial class GenericCategorization
{
    private RegexRuleBuilder _rules;
    private readonly bool _debugProcessSingleDescription;
    private readonly int _debugLengthThreshold;

    public GenericCategorization(
        RegexRuleBuilder rules,
        bool debugProcessSingleDescription = false, 
        int debugLengthThreshold = 40)
    {
        _rules = rules;
        _debugProcessSingleDescription = debugProcessSingleDescription;
    }

    /// <summary>
    /// Se <paramref name="buildGraph"/> = <c>false</c> il grafo NON viene costruito
    /// (più veloce, nessuna dipendenza da Neo4j).
    /// </summary>
    public async Task<(List<Description> descriptions,
                       TransformationGraph? graph)> CategorizeDescriptionsAsync(
                       List<string> rawDescriptions,
                       bool buildGraph = true)
    {
        // 1) converte input in Description
        var descriptions = rawDescriptions
            .Select(d => new Description(d))
            .ToList();

        // 2) prepara regole
        var rules = _rules.Build()
                          .Cast<TransformationRuleBase>()
                          .ToList();

        // 3) esegue il motore (NON blocca l’UI)
        var engine = new TransformationEngine();
        var records = await Task.Run(() =>
                        engine.Run(descriptions, rules).ToList());

        // 4) opzionalmente costruisce il grafo
        TransformationGraph? graph = null;
        if (buildGraph)
        {
            var builder = new GraphBuilderFromRecords();
            var (nodes, edges) = builder.Build(records);

            graph = new TransformationGraph
            {
                Nodes = nodes.ToList(),
                Edges = edges.ToList()
            };
        }

        // — DEBUG single description (opzionale) —
        if (_debugProcessSingleDescription && graph is not null)
        {
            foreach (var d in descriptions.Where(d => d.CurrentDescription.Length > 40 /* _debugProcessSingleDescription */))
                Console.WriteLine(GenerateDebugReportForTransaction(d, graph));
        }

        return (descriptions, graph);
    }

    #region ––––– DEBUG report (immutato) –––––
    //  tutto il tuo metodo GenerateDebugReportForTransaction rimane invariato
    #endregion

    #region ––––– Utility per Cypher (facoltativa) –––––
    public static void GenerateAndLogCypherQueries(TransformationGraph graph,
                                                   List<Description> desc)
    {
        var queryGenerator = new CypherQueryGenerator(graph);
        string cypher = queryGenerator.GenerateCypherQueries();
        Console.WriteLine("Cypher Queries:\n" + cypher);

        string sample = JsonSerializer.Serialize(
            desc.Where(d => d.CurrentDescription.Length > 20),
            new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(sample);
    }
    #endregion

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

                if (edge.Rule is IRegexRuleMetadata metadata)
                {
                    sb.AppendLine(metadata.ToDebugString());
                }

                sb.AppendLine($"    Input: {edge.DebugData.Input}");
                sb.AppendLine($"    Output: {edge.DebugData.Output}");
                sb.AppendLine($"    Matched: {edge.DebugData.IsMatch}");
                // Se in futuro si aggiunge la misurazione del tempo, ad esempio:
                // sb.AppendLine($"    Time Taken: {edge.DebugData.TimeTaken} ms");
                sb.AppendLine($"    TransformationCount (Rule): {((RegexTransformationRule)edge.DebugData.Rule).Count}");
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
                if (aggEdge.Rule is IRegexRuleMetadata metadata)
                {
                    sb.AppendLine(metadata.ToDebugString());
                }
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
}