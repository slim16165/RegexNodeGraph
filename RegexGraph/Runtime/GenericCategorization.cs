using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using RegexNodeGraph.Runtime.Graph;
using RegexNodeGraph.Runtime.Graph.Model;

namespace RegexNodeGraph.Runtime;

public class GenericCategorization
{
    public RegexRuleBuilder Rules { get; set; }

    public GenericCategorization(RegexRuleBuilder rules)
    {
        Rules = rules;
    }

    public async Task<(List<Description> transactionDesc, RegexTransformationGraph graph)> CategorizeDescriptions(List<string> descriptions)
    {
        // Converti le descrizioni uniche in TransactionDescription
        List<Description> transactionDescriptions = descriptions
            .Select(desc => new Description(desc))
            .ToList();

        RegexTransformationGraph graph = new RegexTransformationGraph();
        List<RegexDescription> regexDescriptions = Rules.Build();

        //Solo per debug
        bool debugTransform1DescrOnly = true;

        await Task.Run(() =>
        {
            if (!debugTransform1DescrOnly)
            {
                graph.BuildGraph(transactionDescriptions, Rules.Build());
            }
            else //Solo per debug
            {
                for (int i = 1; i < transactionDescriptions.Count; i++)
                {
                    List<Description> description = [transactionDescriptions[i]];

                    // Costruisco il grafo per questa singola transazione
                    graph.BuildGraph(description, regexDescriptions);

                    if (description[0].CurrentDescription.Length > 40)
                    {
                        // Ora estraiamo i dati di debug dal grafo e dalle descrizioni
                        string debugReport = GenerateDebugReportForTransaction(description[0], graph);

                        // Stampa il report in console o salva in un file di log
                        Console.WriteLine(debugReport);

                        //Debugger.Break();
                    }
                }
            }
        });
        return (transactionDescriptions, graph);
    }

    //TODO: questo metodo dovrebbe passare sul chiamante, così non servirebbero più func ed action

    public static async Task SetCategoryOnOriginalItems<T>(List<T> items, Func<T, string> getDescription, Action<T, string> setCategory,
        List<Description> transactionDescriptions, RegexTransformationGraph graph)
    {
        await Task.Run(() =>
        {
            //Questo metodo usa il grafo per individuare la categoria finale (il grafo è stato calcolato prima)
            Parallel.ForEach(items, item =>
            {
                string originalDescription = getDescription(item); //item è la transazione bancaria, il metodo però è generico trn => trn.Description,
                
                Description transaction = transactionDescriptions.FirstOrDefault(t => t.OriginalDescription == originalDescription);
                if (transaction != null)
                {
                    setCategory(item, transaction.Category);
                }

                return;

                string category = RegexTransformationGraph.UpdateOriginalItemsWithFinalCategory(originalDescription, graph);
                setCategory(item, category); //(trn, category) => trn.Category = category
            });
        });
    }

    public static string GenerateDebugReportForTransaction(Description transaction, RegexTransformationGraph graph)
    {
        // Troviamo il DetailedTransactionNode relativo a questa transazione
        var detailedNode = graph.Nodes
            .OfType<DetailedTransactionNode>()
            .FirstOrDefault(n => n.Description == transaction);

        if (detailedNode == null)
        {
            return $"No detailed node found for transaction: {transaction.OriginalDescription}";
        }

        var sb = new System.Text.StringBuilder();

        sb.AppendLine("=== DEBUG REPORT FOR TRANSACTION ===");
        sb.AppendLine($"Original Description: {transaction.OriginalDescription}");
        sb.AppendLine($"Final Description: {transaction.CurrentDescription}");
        sb.AppendLine($"Is Categorized: {transaction.IsCategorized}");
        if (transaction.IsCategorized && !string.IsNullOrEmpty(transaction.Category))
        {
            sb.AppendLine($"Assigned Category: {transaction.Category}");
        }
        sb.AppendLine();

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
            foreach (var edge in detailEdges)
            {
                sb.AppendLine($"  Regex From: {edge.RegexRule.From}");
                sb.AppendLine($"  Regex To: {edge.RegexRule.To}");
                sb.AppendLine($"  Description: {edge.RegexRule.Description}");
                sb.AppendLine($"  Categories: {string.Join(", ", edge.RegexRule.Categories)}");
                sb.AppendLine($"  Input: {edge.DebugData.Input}");
                sb.AppendLine($"  Output: {edge.DebugData.Output}");
                sb.AppendLine($"  Matched: {edge.DebugData.IsMatch}");
                sb.AppendLine($"  TransformationCount (Rule): {edge.DebugData.Regex.Count}");
                sb.AppendLine();
            }
        }

        // Se vogliamo tracciare anche le trasformazioni aggregate (AggregatedTransactionsNode)
        // Possiamo guardare i TransformationEdge
        var transformationEdges = graph.Edges
            .OfType<TransformationEdge>()
            .Where(e => e.SourceNode is AggregatedTransactionsNode && e.TargetNode is AggregatedTransactionsNode)
            .ToList();

        if (transformationEdges.Any())
        {
            sb.AppendLine("Aggregated Transformations:");
            foreach (var aggEdge in transformationEdges)
            {
                sb.AppendLine($"  Regex From: {aggEdge.RegexRule.From}");
                sb.AppendLine($"  Regex To: {aggEdge.RegexRule.To}");
                sb.AppendLine($"  Description: {aggEdge.RegexRule.Description}");
                sb.AppendLine($"  Categories: {string.Join(", ", aggEdge.RegexRule.Categories)}");
                sb.AppendLine($"  TransformationCount (edge): {aggEdge.TransformationCount}");

                // DebugData di più item, se presenti
                foreach (var d in aggEdge.DebugData)
                {
                    sb.AppendLine($"    Input: {d.Input}");
                    sb.AppendLine($"    Output: {d.Output}");
                    sb.AppendLine($"    Matched: {d.IsMatch}");
                    sb.AppendLine();
                }
                sb.AppendLine();
            }
        }

        sb.AppendLine("=== END OF DEBUG REPORT ===");

        return sb.ToString();
    }

    public static void GenerateAndLogCypherQueries(RegexTransformationGraph graph, List<Description> transactionDescriptions)
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