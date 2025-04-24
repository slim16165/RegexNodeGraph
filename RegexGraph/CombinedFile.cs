### Contenuto di GenericCategorization.cs ###
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
                sb.AppendLine($"    Regex From: {edge.TransformationRule.From}");
                sb.AppendLine($"    Regex To: {edge.TransformationRule.To}");
                sb.AppendLine($"    Rule Description: {edge.TransformationRule.Description}");
                sb.AppendLine($"    Categories: {string.Join(", ", edge.TransformationRule.Categories)}");
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

### Contenuto di ConfigOptions.cs ###
using System;

namespace RegexNodeGraph;

[Flags]
public enum ConfigOptions
{
    NonUscireInCasoDiMatch = 1,
    EsciInCasoDiMatch = 2,
    IgnoraRetry = 4
}

### Contenuto di Neo4jConnection.cs ###
using System;
using Neo4j.Driver;

namespace RegexNodeGraph.Connection;

public class Neo4jConnection : IDisposable
{
    private readonly IDriver _driver;

    public IDriver Driver => _driver;

    public Neo4jConnection(string uri, string user, string password)
    {
        _driver = GraphDatabase.Driver(uri, AuthTokens.Basic(user, password));
    }

    public void Dispose()
    {
        _driver?.Dispose();
    }
}

### Contenuto di AggregatedTransactionsNode.cs ###
using System.Collections.Generic;
using RegexNodeGraph.Model;

namespace RegexNodeGraph.Graph.GraphCore;

public class AggregatedTransactionsNode : GraphNode
{
    public List<Description> Descriptions { get; set; } = new List<Description>();
    public int Cardinality => Descriptions.Count;
    public List<TransformationEdge> OutgoingEdges { get; set; } = new List<TransformationEdge>();
    public List<string> Categories { get; set; }
}

### Contenuto di DetailedTransactionNode.cs ###
using RegexNodeGraph.Model;

namespace RegexNodeGraph.Graph.GraphCore;

public class DetailedTransactionNode : GraphNode
{
    public Description Description { get; set; }
    public string Category { get; set; }
}

### Contenuto di DetailTransformationEdge.cs ###
using RegexNodeGraph.Model;

namespace RegexNodeGraph.Graph.GraphCore;

public class DetailTransformationEdge : GraphEdge
{
    public ITransformationRule TransformationRule { get; set; }
    public TransformationDebugData DebugData { get; set; }
    // Trasformazioni a livello di singola transazione
}

### Contenuto di GraphEdge.cs ###
namespace RegexNodeGraph.Graph.GraphCore;

public abstract class GraphEdge
{
    public GraphNode SourceNode { get; set; }
    public GraphNode TargetNode { get; set; }
}

### Contenuto di GraphNode.cs ###
namespace RegexNodeGraph.Graph.GraphCore;

public abstract class GraphNode
{
    public int Id { get; set; }
}

### Contenuto di MembershipEdge.cs ###
namespace RegexNodeGraph.Graph.GraphCore;

public class MembershipEdge : GraphEdge
{
    // Rappresenta l'appartenenza di un nodo di dettaglio a un nodo aggregato
    // Puoi aggiungere proprietà se necessario
}

### Contenuto di TransformationEdge.cs ###
using System.Collections.Generic;
using RegexNodeGraph.Model;
using RegexNodeGraph.RegexRules;

namespace RegexNodeGraph.Graph.GraphCore;

public class TransformationEdge : GraphEdge
{
    public RegexTransformationRule RegexRule { get; set; }
    public int TransformationCount { get; set; }
    public List<TransformationDebugData> DebugData { get; set; } = new List<TransformationDebugData>();
}

### Contenuto di CypherQueryGenerator.cs ###
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RegexNodeGraph.Graph.GraphCore;

namespace RegexNodeGraph.Graph.Processing;

public class CypherQueryGenerator
{
    private readonly TransformationGraph _graph;
    private readonly StringBuilder _sb = new StringBuilder();

    public CypherQueryGenerator(TransformationGraph graph)
    {
        _graph = graph;
    }

    public string GenerateCypherQueries()
    {
        _sb.Clear();

        // Aggiungi una query per svuotare il database
        _sb.AppendLine("// Svuota il database");
        _sb.AppendLine("MATCH (n)");
        _sb.AppendLine("DETACH DELETE n;\n");

        // Generazione dei vincoli di unicità
        GenerateConstraints();

        // Raccolta dei dati dei nodi
        var detailedNodes = new List<string>();
        var aggregatedNodes = new List<string>();

        foreach (var node in _graph.Nodes)
        {
            if (node is AggregatedTransactionsNode aggNode)
            {
                // Preparazione dei dati per AggregatedNode
                int descriptionsCount = aggNode.Descriptions.Count;
                aggregatedNodes.Add($"{{id: {aggNode.Id}, descriptions_count: {descriptionsCount}}}");
            }
            else if (node is DetailedTransactionNode detailNode)
            {
                // Escaping delle virgolette nella descrizione
                var escapedDescription = detailNode.Description.CurrentDescription.Replace("\"", "\\\"");
                detailedNodes.Add($"{{id: {detailNode.Id}, description: \"{escapedDescription}\"}}");
            }
        }

        // Generazione batch di nodi DetailedNode
        if (detailedNodes.Any())
        {
            _sb.AppendLine("// Creazione batch di DetailedNode");
            _sb.AppendLine("UNWIND [");
            _sb.AppendLine(string.Join(",\n", detailedNodes.Select(dn => $"  {dn}")));
            _sb.AppendLine("] AS nodeData");
            _sb.AppendLine("CREATE (n:DetailedNode {id: nodeData.id, description: nodeData.description});\n");
        }

        // Generazione batch di nodi AggregatedNode
        if (aggregatedNodes.Any())
        {
            _sb.AppendLine("// Creazione batch di AggregatedNode");
            _sb.AppendLine("UNWIND [");
            _sb.AppendLine(string.Join(",\n", aggregatedNodes.Select(an => $"  {an}")));
            _sb.AppendLine("] AS nodeData");
            _sb.AppendLine("CREATE (n:AggregatedNode {id: nodeData.id, descriptions_count: nodeData.descriptions_count});\n");
        }

        // Raccolta dei dati delle relazioni
        var belongsToRelations = new List<string>();
        var aggregatedTransformsRelations = new List<string>();
        var detailTransformsRelations = new List<string>();

        foreach (var edge in _graph.Edges)
        {
            if (edge is MembershipEdge memberEdge)
            {
                belongsToRelations.Add($"{{fromId: {memberEdge.SourceNode.Id}, toId: {memberEdge.TargetNode.Id}}}");
            }
            else if (edge is TransformationEdge transEdge)
            {
                var escapedRegex = transEdge.RegexRule.From.Replace("'", "\\'");
                aggregatedTransformsRelations.Add($"{{fromId: {transEdge.SourceNode.Id}, toId: {transEdge.TargetNode.Id}, regex: '{escapedRegex}', count: {transEdge.TransformationCount}}}");
            }
            else if (edge is DetailTransformationEdge detailEdge)
            {
                var escapedRegex = detailEdge.TransformationRule.From.Replace("'", "\\'");
                detailTransformsRelations.Add($"{{fromId: {detailEdge.SourceNode.Id}, toId: {detailEdge.TargetNode.Id}, regex: '{escapedRegex}'}}");
            }
        }

        belongsToRelations = belongsToRelations.Distinct().ToList();

        // Generazione batch di relazioni BELONGS_TO
        if (belongsToRelations.Any())
        {
            _sb.AppendLine("// Creazione batch di relazioni BELONGS_TO");
            _sb.AppendLine("UNWIND [");
            _sb.AppendLine(string.Join(",\n", belongsToRelations.Select(br => $"  {br}")));
            _sb.AppendLine("] AS relData");
            _sb.AppendLine("MATCH (from:DetailedNode {id: relData.fromId})");
            _sb.AppendLine("MATCH (to:AggregatedNode {id: relData.toId})");
            _sb.AppendLine("CREATE (from)-[:BELONGS_TO]->(to);\n");
        }

        // Generazione batch di relazioni AGGREGATED_TRANSFORMS
        if (aggregatedTransformsRelations.Any())
        {
            _sb.AppendLine("// Creazione batch di relazioni AGGREGATED_TRANSFORMS");
            _sb.AppendLine("UNWIND [");
            _sb.AppendLine(string.Join(",\n", aggregatedTransformsRelations.Select(ar => $"  {ar}")));
            _sb.AppendLine("] AS relData");
            _sb.AppendLine("MATCH (from:AggregatedNode {id: relData.fromId})");
            _sb.AppendLine("MATCH (to:AggregatedNode {id: relData.toId})");
            _sb.AppendLine("CREATE (from)-[:AGGREGATED_TRANSFORMS {regex: relData.regex, count: relData.count}]->(to);\n");
        }

        // Generazione batch di relazioni DETAIL_TRANSFORMS
        if (detailTransformsRelations.Any())
        {
            _sb.AppendLine("// Creazione batch di relazioni DETAIL_TRANSFORMS");
            _sb.AppendLine("UNWIND [");
            _sb.AppendLine(string.Join(",\n", detailTransformsRelations.Select(dr => $"  {dr}")));
            _sb.AppendLine("] AS relData");
            _sb.AppendLine("MATCH (from:DetailedNode {id: relData.fromId})");
            _sb.AppendLine("MATCH (to:DetailedNode {id: relData.toId})");
            _sb.AppendLine("CREATE (from)-[:DETAIL_TRANSFORMS {regex: relData.regex}]->(to);\n");
        }

        // Aggiungi il calcolo del nodo root
        _sb.AppendLine("// Calcolo del nodo root");
        _sb.AppendLine("MATCH (n:AggregatedNode)");
        _sb.AppendLine("WHERE NOT (n)<-[:AGGREGATED_TRANSFORMS]-()");
        _sb.AppendLine("SET n.is_root = true;\n");

        // Dopo aver generato i nodi e le relazioni, aggiungi il calcolo degli step trasformativi
        _sb.AppendLine("// Calcolo degli step trasformativi dal nodo root");
        _sb.AppendLine("MATCH path = (root:AggregatedNode {is_root: true})-[:AGGREGATED_TRANSFORMS*]->(n:AggregatedNode)");
        _sb.AppendLine("WITH n, length(path) AS steps");
        _sb.AppendLine("SET n.steps_from_root = steps;\n");


        return _sb.ToString();
    }

    private void GenerateConstraints()
    {
        _sb.AppendLine("// Vincoli di unicità");
        _sb.AppendLine("CREATE CONSTRAINT IF NOT EXISTS FOR (n:DetailedNode) REQUIRE n.id IS UNIQUE;");
        _sb.AppendLine("CREATE CONSTRAINT IF NOT EXISTS FOR (n:AggregatedNode) REQUIRE n.id IS UNIQUE;\n");
    }

    // Metodo helper per ottenere l'etichetta del nodo
    private static string GetLabel(GraphNode node)
    {
        return node switch
        {
            AggregatedTransactionsNode _ => "AggregatedNode",
            DetailedTransactionNode _ => "DetailedNode",
            _ => "Node"
        };
    }
}

### Contenuto di GraphBuilder.cs ###
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Neo4j.Driver;
using RegexNodeGraph.Graph.GraphCore;
using RegexNodeGraph.RegexRules;

namespace RegexNodeGraph.Graph.Processing;

public class GraphBuilder
{
    // Lista per mantenere le query e i parametri
    private readonly List<(string query, Dictionary<string, object> parameters)> _queries = new();
    private readonly StringBuilder _sb = new StringBuilder();

    // Flag per indicare se si sta generando solo testo (senza connessione)
    private readonly bool _isTextualMode;

    public GraphBuilder(bool isTextualMode = true)
    {
        _isTextualMode = isTextualMode;
    }

    public void AddNode(AggregatedTransactionsNode node)
    {
        if (_isTextualMode)
        {
            var descriptionList = string.Join(", ", node.Descriptions.Select(d => $"\"{d}\""));
            var categoryList = node.Categories != null
                ? string.Join(", ", node.Categories.Select(c => $"\"{c}\""))
                : string.Empty;

            _sb.AppendLine($"CREATE (n{node.Id}:TransactionNode {{id: {node.Id}, cardinality: {node.Cardinality}, descriptions: [{descriptionList}], categories: [{categoryList}]}});");
        }
        else
        {
            var query = @"
            CREATE (n:TransactionNode {id: $id, cardinality: $cardinality, descriptions: $descriptions, categories: $categories})
        ";

            var parameters = new Dictionary<string, object>
            {
                {"id", node.Id},
                {"cardinality", node.Cardinality},
                {"descriptions", node.Descriptions},
                {"categories", node.Categories}
            };

            _queries.Add((query, parameters));
        }
    }

    // Metodo per aggiungere un arco
    public void AddEdge(int sourceId, int targetId, string regex, int transformationCount)
    {
        if (_isTextualMode)
        {
            var escapedRegex = regex.Replace("'", "\\'");
            _sb.AppendLine($"MATCH (n{sourceId}:TransactionNode), (n{targetId}:TransactionNode)");
            _sb.AppendLine($"CREATE (n{sourceId})-[:TRANSFORMS {{regex: '{escapedRegex}', count: {transformationCount}}}]->(n{targetId});");

        }
        else
        {
            var query = @"
                    MATCH (n1:TransactionNode {id: $sourceId}), (n2:TransactionNode {id: $targetId})
                    CREATE (n1)-[:TRANSFORMS {regex: $regex, count: $transformationCount}]->(n2)
                ";

            var parameters = new Dictionary<string, object>
            {
                {"sourceId", sourceId},
                {"targetId", targetId},
                {"regex", regex},
                {"transformationCount", transformationCount}
            };

            _queries.Add((query, parameters));
        }
    }

    public void MergeCategory(string categoryName)
    {
        if (_isTextualMode)
        {
            _sb.AppendLine($"MERGE (c:Category {{name: \"{categoryName}\"}});");
        }
        else
        {
            var query = @"
            MERGE (c:Category {name: $categoryName})
        ";

            var parameters = new Dictionary<string, object>
            {
                {"categoryName", categoryName}
            };

            _queries.Add((query, parameters));
        }
    }

    public void AssignCategory(int transactionId, string categoryName)
    {
        if (_isTextualMode)
        {
            _sb.AppendLine($"MATCH (t:TransactionNode {{id: {transactionId}}}), (c:Category {{name: \"{categoryName}\"}})");
            _sb.AppendLine($"WITH t, c");
            _sb.AppendLine($"CREATE (t)-[:ASSIGNED_TO]->(c);");
        }
        else
        {
            var query = @"
            MATCH (t:TransactionNode {id: $transactionId}), (c:Category {name: $categoryName})
            CREATE (t)-[:ASSIGNED_TO]->(c)
        ";

            var parameters = new Dictionary<string, object>
            {
                {"transactionId", transactionId},
                {"categoryName", categoryName}
            };

            _queries.Add((query, parameters));
        }
    }





    // Metodo per eseguire le query su Neo4j in batch
    public async Task ExecuteQueriesAsync(IDriver driver, int batchSize = 1000)
    {
        if (_isTextualMode)
        {
            // In modalità testuale, non eseguiamo alcuna query
            return;
        }

        using var session = driver.AsyncSession();
        try
        {
            int totalQueries = _queries.Count;
            for (int i = 0; i < totalQueries; i += batchSize)
            {
                var batch = _queries.Skip(i).Take(batchSize).ToList();

                await session.WriteTransactionAsync(async tx =>
                {
                    foreach (var (query, parameters) in batch)
                    {
                        await tx.RunAsync(query, parameters);
                    }
                });
            }
        }
        finally
        {
            await session.CloseAsync();
        }
    }

    public override string ToString()
    {
        return _sb.ToString();
    }





    // Metodo per aggiungere un nodo di transazione bancaria singola
    public void AddDetailNode_BankTransaction(string transactionDescription)
    {
        if (_isTextualMode)
        {
            // Parte di dettaglio: Crea un nodo individuale per ogni singola transazione (bank_transaction)
            _sb.AppendLine($"MERGE (:BankTransaction {{description: \"{transactionDescription}\"}});");
        }
        else
        {
            // Query per creare il nodo "BankTransaction" su Neo4j
            var query = @"
            MERGE (n:BankTransaction {description: $description})
        ";

            var parameters = new Dictionary<string, object>
            {
                {"description", transactionDescription}
            };

            _queries.Add((query, parameters));
        }
    }

    public void AddDetailEdge(string originalTransaction, string transformedTransaction, RegexTransformationRule rule)
    {
        if (_isTextualMode)
        {
            // Crea un MATCH tra il nodo originale e quello trasformato e aggiungi l'arco TRANSFORMS
            _sb.AppendLine($@"MATCH (n1:BankTransaction {{description: ""{originalTransaction}""}}), (n2:BankTransaction {{description: ""{transformedTransaction}""}})
        CREATE (n1)-[:TRANSFORMS {{regex: '{rule.From}'}}]->(n2);
        ");
        }
        else
        {
            var query = @"
        MATCH (n1:BankTransaction {description: $originalTransaction}), (n2:BankTransaction {description: $transformedTransaction})
        CREATE (n1)-[:TRANSFORMS {regex: $regex}]->(n2)
        ";

            var parameters = new Dictionary<string, object>
            {
                {"originalTransaction", originalTransaction},
                {"transformedTransaction", transformedTransaction},
                {"regex", rule.From}
            };

            _queries.Add((query, parameters));
        }
    }
}

### Contenuto di TransformationGraph.cs ###
using System;
using System.Collections.Generic;
using System.Linq;
using RegexNodeGraph.Graph.GraphCore;
using RegexNodeGraph.Model;
using RegexNodeGraph.RegexRules;

namespace RegexNodeGraph.Graph.Processing;

public class TransformationGraph
{
    private readonly object _lock = new object();
    public List<GraphNode> Nodes { get; private set; } = new List<GraphNode>();
    public List<GraphEdge> Edges { get; private set; } = new List<GraphEdge>();

    public void BuildGraph(List<Description> descriptions, List<RegexTransformationRule> regexes)
    {
        ClearGraph();

        var detailNodes = CreateDetailedTransactionNodes(descriptions);
        var rootNode = CreateAggregatedTransactionsNode(descriptions, detailNodes);
        ProcessNode(rootNode, regexes);
    }

    private void ClearGraph()
    {
        lock (_lock)
        {
            Nodes.Clear();
            Edges.Clear();
        }
    }

    private List<DetailedTransactionNode> CreateDetailedTransactionNodes(List<Description> descriptions)
    {
        return descriptions.Select(desc => CreateDetailedTransactionNode(desc)).ToList();
    }

    private DetailedTransactionNode CreateDetailedTransactionNode(Description description)
    {
        var node = new DetailedTransactionNode
        {
            Id = GenerateUniqueId(),
            Description = description
        };

        lock (_lock)
        {
            Nodes.Add(node);
        }

        return node;
    }

    private AggregatedTransactionsNode CreateAggregatedTransactionsNode(List<Description> descriptions, List<DetailedTransactionNode> detailNodes)
    {
        AggregatedTransactionsNode node;
        lock (_lock)
        {
            node = new AggregatedTransactionsNode
            {
                Id = GenerateUniqueId(),
                Descriptions = descriptions
            };
            Nodes.Add(node);
        }

        // Separare la creazione degli archi dalla creazione del nodo
        // PARTE DETTAGLIO
        // Creazione degli archi di appartenenza tra nodi di dettaglio e il nodo aggregato
        // Per ciascuna transazione bancaria, creiamo un nodo di dettaglio (bank_transaction)
        AddMembershipEdges(detailNodes, node);

        return node;
    }

    private void AddMembershipEdges(List<DetailedTransactionNode> detailNodes, AggregatedTransactionsNode aggregatedNode)
    {
        foreach (var detailNode in detailNodes)
        {
            AddMembershipEdge(detailNode, aggregatedNode);
        }
    }

    public void AddMembershipEdge(DetailedTransactionNode detailNode, AggregatedTransactionsNode aggregatedNode)
    {
        var edge = new MembershipEdge
        {
            SourceNode = detailNode,
            TargetNode = aggregatedNode
        };
        AddEdge(edge);
    }

    public void AddAggregatedTransformationEdge(AggregatedTransactionsNode source, AggregatedTransactionsNode target, RegexTransformationRule rule, int count, List<TransformationDebugData> debugData)
    {
        var edge = new TransformationEdge
        {
            SourceNode = source,
            TargetNode = target,
            RegexRule = rule,
            TransformationCount = count,
            DebugData = debugData
        };
        AddEdge(edge);
    }

    private void AddDetailTransformationEdge(DetailedTransactionNode sourceNode, DetailedTransactionNode targetNode, RegexTransformationRule rule, TransformationDebugData debugData)
    {
        var edge = new DetailTransformationEdge
        {
            SourceNode = sourceNode,
            TargetNode = targetNode,
            TransformationRule = rule,
            DebugData = debugData
        };
        AddEdge(edge);
    }

    private int _currentId = 0;
    private int GenerateUniqueId()
    {
        lock (_lock)
        {
            return _currentId++;
        }
    }

    // Implementazione del metodo AddEdge centralizzato
    private void AddEdge(GraphEdge edge)
    {
        lock (_lock)
        {
            // Evita di creare un self-loop
            if (edge.SourceNode.Id == edge.TargetNode.Id)
            {
                string regexFrom = edge is TransformationEdge te ? te.RegexRule.From : edge is DetailTransformationEdge de ? de.TransformationRule.From : "";
                Console.WriteLine($"Self-loop detected and avoided for node {edge.SourceNode.Id} with regex '{regexFrom}'");
                return;
            }

            Edges.Add(edge);

            // Aggiungi l'arco all'elenco degli archi in uscita del nodo, se applicabile
            if (edge is TransformationEdge transformationEdge && transformationEdge.SourceNode is AggregatedTransactionsNode aggSource)
            {
                aggSource.OutgoingEdges.Add(transformationEdge);
            }
        }
    }

    private void ProcessNode(AggregatedTransactionsNode node, List<RegexTransformationRule> rules)
    {
        foreach (var rule in rules)
        {
            // Filtriamo le descrizioni che devono essere saltate
            var skippedDescriptions = node.Descriptions
                .Where(description => description.IsCategorized && rule.EsciInCasoDiMatch)
                .ToList();

            // Applichiamo la regola alle descrizioni restanti
            var processedItems = node.Descriptions
                .Where(description => !(description.IsCategorized && rule.EsciInCasoDiMatch))
                .Select(description =>
                {
                    var (modifiedDescr, result) = ApplyRule(rule, description);
                    return new { OriginalDescription = description, ModifiedDescription = modifiedDescr, DebugInfo = result };
                })
                .ToList();

            // Separiamo le descrizioni trasformate da quelle non trasformate
            var transformedItems = processedItems
                .Where(item => item.ModifiedDescription.HasChangedNow)
                .ToList();

            var unchangedItems = processedItems
                .Where(item => !item.ModifiedDescription.HasChangedNow)
                .Select(item => item.OriginalDescription)
                .Concat(skippedDescriptions)
                .ToList();

            // Se non ci sono trasformazioni, passiamo alla prossima regola
            if (!transformedItems.Any())
                continue;

            // Creazione dei nodi di dettaglio trasformati
            var transformedDetailNodes = transformedItems
                .Select(item => CreateDetailedTransactionNode(item.ModifiedDescription))
                .ToList();

            // Creazione del nuovo nodo aggregato per le descrizioni trasformate
            var transformedDescriptions = transformedItems.Select(item => item.ModifiedDescription).ToList();
            var targetNode = CreateAggregatedTransactionsNode(transformedDescriptions, transformedDetailNodes);

            // Aggiunta dell'edge di trasformazione aggregata
            var debugData = transformedItems.Select(item => item.DebugInfo).ToList();
            AddAggregatedTransformationEdge(node, targetNode, rule, transformedDescriptions.Count, debugData);

            // Gestione delle descrizioni non trasformate
            // Le transazioni non trasformate rimangono nel nodo aggregato originale
            HandleUnchangedDescriptions(unchangedItems, node);

            // Gestione delle trasformazioni a livello di dettaglio
            var detailTransformations = transformedItems
                .Select(item => (item.OriginalDescription, item.ModifiedDescription, item.DebugInfo))
                .ToList();

            HandleDetailTransformations(detailTransformations, rule);

            // Assegnazione delle transazioni trasformate al nuovo nodo aggregato
            AssignTransformedNodesToAggregated(transformedDetailNodes, targetNode);

            // Ricorsione sulle transazioni trasformate
            ProcessNode(targetNode, rules.Skip(rules.IndexOf(rule) + 1).ToList());
        }
    }


    private void HandleUnchangedDescriptions(List<Description> unchangedDescriptions, AggregatedTransactionsNode originalNode)
    {
        foreach (var unchangedDesc in unchangedDescriptions)
        {
            DetailedTransactionNode detailNode;
            lock (_lock)
            {
                detailNode = Nodes.OfType<DetailedTransactionNode>().FirstOrDefault(n => n.Description == unchangedDesc);
            }
            if (detailNode != null)
            {
                AddMembershipEdge(detailNode, originalNode); // Appartenenza a nodo aggregato originale
            }
        }
    }

    private void HandleDetailTransformations(List<(Description sourceDesc, Description targetDesc, TransformationDebugData debugInfo)> detailTransformations, RegexTransformationRule rule)
    {
        foreach (var (sourceDesc, targetDesc, debugInfo) in detailTransformations)
        {
            DetailedTransactionNode sourceNode;
            DetailedTransactionNode targetNodeDetail;
            lock (_lock)
            {
                sourceNode = Nodes.OfType<DetailedTransactionNode>().First(n => n.Description == sourceDesc);
                targetNodeDetail = Nodes.OfType<DetailedTransactionNode>().FirstOrDefault(n => n.Description == targetDesc);
                if (targetNodeDetail == null)
                {
                    targetNodeDetail = CreateDetailedTransactionNode(targetDesc);
                }
            }

            AddDetailTransformationEdge(sourceNode, targetNodeDetail, rule, debugInfo);
        }
    }

    private void AssignTransformedNodesToAggregated(List<DetailedTransactionNode> transformedNodes, AggregatedTransactionsNode aggregatedNode)
    {
        foreach (var transformedNode in transformedNodes)
        {
            AddMembershipEdge(transformedNode, aggregatedNode);
        }
    }

    private (Description description, TransformationDebugData result) ApplyRule(RegexTransformationRule rule, Description description)
    {
        // Applichiamo la sostituzione regex, che aggiorna la descrizione se c'è un match
        (var regexDebugData, description) = rule.ApplyReplacement(description);

        //Se non è cambiata, valuta se devi testare un retry dall'originale
        if (!description.HasChangedNow)
        {
            if (description.WasEverModified && rule.ShouldRetryFromOrig)
            {
                return HandleRetryFromOriginal(rule, description);
            }
        }

        // Altrimenti, ritorniamo la descrizione corrente (modificata o meno)
        return (description, regexDebugData);
    }

    private (Description description, TransformationDebugData result) HandleRetryFromOriginal(RegexTransformationRule rule, Description description)
    {
        var simulatedResult = rule.SimulateApplication(description.OriginalDescription);

        if (simulatedResult.IsMatch)
        {
            var breakingRules = FindInterferingRules(description.OriginalDescription, rule).ToList();
            if (breakingRules.Any())
            {
                string log = $"La regola '{breakingRules[0].From}' (applicata prima) sta interferendo con la regola corrente '{rule.From}'. L'input originale era '{description.OriginalDescription}'.";
                // Log o gestione dell'interferenza
                // Utilizza un logger appropriato invece di Console.WriteLine
                Console.WriteLine(log);
            }
        }

        // Aggiorniamo la descrizione solo se c'è stato un match simulato
        if (simulatedResult.IsMatch)
        {
            description.UpdateDescription(simulatedResult.Output);
        }

        return (description, simulatedResult);
    }

    public IEnumerable<RegexTransformationRule> FindInterferingRules(string input, RegexTransformationRule rule2)
    {
        var rules = Edges.OfType<TransformationEdge>().Select(e => e.RegexRule).Distinct().ToList();
        foreach (RegexTransformationRule rule1 in rules)
        {
            if (rule1 == rule2)
                break;

            var result1 = rule1.ApplyReplacement(input);

            if (!result1.IsMatch || result1.Output == input)
            {
                continue;
            }
            else
            {
                var result2 = rule2.ApplyReplacement(result1.Output);
                if (!result2.IsMatch)
                    yield return rule1;
            }
        }
    }

    /// <summary>
    /// Il metodo cerca di trovare la descrizione finale di una transazione partendo da una descrizione originale e attraversando il grafo di trasformazioni regex. Si ferma quando non ci sono più trasformazioni disponibili o quando un arco specifica l'opzione di interruzione (EsciInCasoDiMatch). Se non viene trovata alcuna corrispondenza iniziale, viene restituito l'input originale.
    /// </summary>
    /// <summary>
    /// Trova la descrizione finale di una transazione attraversando il grafo di trasformazioni regex.
    /// Si ferma quando non ci sono più trasformazioni disponibili o quando un arco specifica l'opzione di interruzione (EsciInCasoDiMatch).
    /// Se non viene trovata alcuna corrispondenza iniziale, ritorna l'input originale.
    /// </summary>
    /// <param name="originalDescription">La descrizione originale della transazione.</param>
    /// <param name="graph">Il grafo di trasformazioni regex.</param>
    /// <returns>La descrizione finale dopo l'applicazione delle trasformazioni.</returns>
    public static string UpdateOriginalItemsWithFinalCategory(string originalDescription, TransformationGraph graph)
    {
        //Recupera il nodo di dettaglio che corrisponde alla descrizione fornita.
        var detailedNode = graph.Nodes
            .OfType<DetailedTransactionNode>()
            .FirstOrDefault(n => n.Description.OriginalDescription == originalDescription);
        
        if (detailedNode == null)
        {
            Console.WriteLine("Nodo dettagliato non trovato. Ritorno input originale.");
            return originalDescription;
        }

        var finalNode = TraverseTransformations(detailedNode, graph);
        Console.WriteLine($"Descrizione finale trovata: \"{finalNode.Description.CurrentDescription}\"");

        return finalNode.Description.CurrentDescription;
    }

    /// <summary>
    /// Trova la descrizione finale di una transazione attraversando il grafo di trasformazioni regex.
    /// Si ferma quando non ci sono più trasformazioni disponibili o quando un arco specifica l'opzione di interruzione (EsciInCasoDiMatch).
    /// Se non viene trovata alcuna corrispondenza iniziale, ritorna l'input originale.
    /// </summary>
    /// <param name="originalInput">La descrizione originale della transazione.</param>
    /// <param name="graph">Il grafo di trasformazioni regex.</param>
    /// <returns>La descrizione finale dopo l'applicazione delle trasformazioni.</returns>
    private static string FindFinalCategory(string originalInput, TransformationGraph graph)
    {
        Console.WriteLine($"Inizio categorizzazione per: \"{originalInput}\"");

        var detailedNode = GetDetailedNodeByDescription(originalInput, graph);
        if (detailedNode == null)
        {
            Console.WriteLine("Nodo dettagliato non trovato. Ritorno input originale.");
            return originalInput;
        }

        var finalNode = TraverseTransformations(detailedNode, graph);
        Console.WriteLine($"Descrizione finale trovata: \"{finalNode.Description.CurrentDescription}\"");

        return finalNode.Description.CurrentDescription;
    }

    /// <summary>
    /// Recupera il nodo di dettaglio che corrisponde alla descrizione fornita.
    /// </summary>
    /// <param name="description">La descrizione della transazione da cercare.</param>
    /// <param name="graph">Il grafo di trasformazioni regex.</param>
    /// <returns>Il nodo di dettaglio corrispondente o null se non trovato.</returns>
    private static DetailedTransactionNode GetDetailedNodeByDescription(string description, TransformationGraph graph)
    {
        var node = graph.Nodes
            .OfType<DetailedTransactionNode>()
            .FirstOrDefault(n => n.Description.CurrentDescription.Equals(description, StringComparison.OrdinalIgnoreCase));

        if (node != null)
        {
            Console.WriteLine($"Nodo dettagliato trovato: ID {node.Id}");
        }
        else
        {
            Console.WriteLine("Nodo dettagliato non trovato.");
        }

        return node;
    }

    /// <summary>
    /// Attraversa le trasformazioni partendo da un nodo di dettaglio fino a trovare la descrizione finale.
    /// </summary>
    /// <param name="startNode">Il nodo di dettaglio iniziale.</param>
    /// <param name="graph">Il grafo di trasformazioni regex.</param>
    /// <returns>Il nodo di dettaglio finale dopo le trasformazioni.</returns>
    private static DetailedTransactionNode TraverseTransformations(DetailedTransactionNode startNode, TransformationGraph graph)
    {
        var currentNode = startNode;
        var visitedNodes = new HashSet<int> { currentNode.Id };
        Console.WriteLine($"Avvio traversata delle trasformazioni per nodo ID {currentNode.Id}");

        while (true)
        {
            var transformationEdge = GetFirstDetailTransformationEdge(currentNode, graph);
            if (transformationEdge == null)
            {
                Console.WriteLine("Nessun ulteriore arco di trasformazione trovato. Traversata terminata.");
                break;
            }

            var targetNode = transformationEdge.TargetNode as DetailedTransactionNode;
            if (targetNode == null)
            {
                Console.WriteLine($"L'arco di trasformazione porta a un nodo non di dettaglio (ID {transformationEdge.TargetNode.Id}). Traversata terminata.");
                break;
            }

            if (visitedNodes.Contains(targetNode.Id))
            {
                Console.WriteLine($"Ciclo rilevato al nodo ID {targetNode.Id}. Traversata terminata.");
                break;
            }

            currentNode = targetNode;
            visitedNodes.Add(currentNode.Id);
            Console.WriteLine($"Trasformazione applicata: Nodo ID {currentNode.Id}, Descrizione: \"{currentNode.Description.CurrentDescription}\"");

            if (ShouldExitOnMatch(transformationEdge.TransformationRule))
            {
                Console.WriteLine("Opzione EsciInCasoDiMatch rilevata. Traversata terminata.");
                break;
            }
        }

        Console.WriteLine($"Traversata completata. Nodo finale ID {currentNode.Id}, Descrizione: \"{currentNode.Description.CurrentDescription}\"");
        return currentNode;
    }

    /// <summary>
    /// Recupera il primo arco di trasformazione di dettaglio associato al nodo corrente.
    /// </summary>
    /// <param name="sourceNode">Il nodo sorgente.</param>
    /// <param name="graph">Il grafo di trasformazioni regex.</param>
    /// <returns>Il primo arco di trasformazione di dettaglio o null se non trovato.</returns>
    private static DetailTransformationEdge GetFirstDetailTransformationEdge(DetailedTransactionNode sourceNode, TransformationGraph graph)
    {
        var edge = graph.Edges
            .OfType<DetailTransformationEdge>()
            .FirstOrDefault(e => e.SourceNode == sourceNode);

        if (edge != null)
        {
            Console.WriteLine($"Arco di trasformazione trovato: RegEx '{edge.TransformationRule.From}', Target Node ID {edge.TargetNode.Id}");
        }
        else
        {
            Console.WriteLine("Nessun arco di trasformazione trovato per il nodo corrente.");
        }

        return edge;
    }

    /// <summary>
    /// Determina se una regola regex specifica di uscire in caso di match.
    /// </summary>
    /// <param name="regexRule">La regola regex da verificare.</param>
    /// <returns>True se la regola specifica di uscire in caso di match, altrimenti False.</returns>
    private static bool ShouldExitOnMatch(RegexTransformationRule regexRule)
    {
        return (regexRule.ConfigOptions & ConfigOptions.EsciInCasoDiMatch) == ConfigOptions.EsciInCasoDiMatch;
    }
}

### Contenuto di Description.cs ###
namespace RegexNodeGraph.Model;

public class Description
{
    public string OriginalDescription { get; set; }
    public string CurrentDescription { get; set; }
    public string Category { get; set; }
    public bool IsCategorized { get; set; } = false;
    public bool HasChangedNow { get; set; } = false; // Indica se la descrizione è cambiata nell'ultimo step
    public bool WasEverModified { get; set; } = false; // Indica se la descrizione è mai stata modificata

    public Description(string description)
    {
        OriginalDescription = description;
        CurrentDescription = description;
        Category = null;
    }

    // Metodo per aggiornare la descrizione mantenendo traccia dello stato precedente
    public void UpdateDescription(string newDescription)
    {
        if (newDescription != CurrentDescription)
        {
            CurrentDescription = newDescription;
            HasChangedNow = true;
            WasEverModified = true;
        }
        else
        {
            HasChangedNow = false;
        }
    }
}

### Contenuto di ITransformationRule.cs ###
using System.Collections.Generic;

namespace RegexNodeGraph.Model;

public interface ITransformationRule
{
    (TransformationDebugData res, Description description) Apply(Description description);
    TransformationDebugData Apply(string input);
    TransformationDebugData Simulate(string input);
}

### Contenuto di TransformationDebugData.cs ###
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace RegexNodeGraph.Model;


/// <summary>
/// Represents the details of a regex replacement operation.
/// </summary>
public class TransformationDebugData
{
    public TransformationDebugData()
    {
    }

    public TransformationDebugData(string input, ITransformationRule regex, string output, bool isMatch)
    {
        Input = input;
        TransformationRule = regex;
        Output = output;
        IsMatch = isMatch;
    }

    public string Input { get; set; }
    public string Output { get; set; }

    public ITransformationRule TransformationRule { get; set; }

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

### Contenuto di TransformationResult.cs ###
namespace RegexNodeGraph.Model;

public record TransformationResult(
    string Input,
    string Output,
    bool IsMatch,
    long ElapsedMs);

### Contenuto di RegexHelper.cs ###
using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using RegexNodeGraph.Model;

namespace RegexNodeGraph.RegexRules;

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

    public static (TransformationDebugData res, Description description) ApplyReplacement(this RegexTransformationRule rule, Description description)
    {
        // Utilizziamo CurrentDescription dalla classe
        string input = description.CurrentDescription ?? string.Empty;
        // Reset del flag HasChangedNow prima dell'applicazione della regola
        description.HasChangedNow = false;

        Stopwatch stopwatch = Stopwatch.StartNew();

        var output = rule.RegexFrom.Replace(input, rule.To);
        bool isMatch = !ReferenceEquals(input, output);   // oppure input != output


        Console.WriteLine($"Applying regex: {rule.RegexFrom} with IgnoreCase: {rule.RegexFrom.Options.HasFlag(RegexOptions.IgnoreCase)}; Input: {input}; Match found: {isMatch}; Output after replacement: {output}");


        stopwatch.Stop();

        var res = new TransformationDebugData(input, rule, output, isMatch);

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


    public static TransformationDebugData ApplyReplacement(this RegexTransformationRule rule, string input)
    {
        input ??= "";
        string output = input;

        Stopwatch stopwatch = Stopwatch.StartNew();

        bool isMatch = rule.RegexFrom.IsMatch(input);
        if (isMatch)
            output = rule.RegexFrom.Replace(input, rule.To);

        stopwatch.Stop();

        var res = new TransformationDebugData(input, rule, output, isMatch);

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

    public static TransformationDebugData SimulateApplication(this RegexTransformationRule r, string input)
    {
        input ??= "";

        string output = r.RegexFrom.Replace(input, r.To);
        bool isMatch = !ReferenceEquals(input, output);   // oppure input != output

        var res = new TransformationDebugData(input, r, output, isMatch);

        return res;
    }
}

### Contenuto di RegexRuleBuilder.cs ###
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace RegexNodeGraph.RegexRules;

public class RegexRuleBuilder : IEnumerable<RegexTransformationRule>
{
    private readonly List<RegexTransformationRule> _rules = new List<RegexTransformationRule>();

    /// <summary>
    /// Aggiunge una nuova regola semplice.
    /// </summary>
    public RegexRuleBuilder Add(string from, string to,
        ConfigOptions configOptions = ConfigOptions.NonUscireInCasoDiMatch)
    {
        _rules.Add(new RegexTransformationRule(from, to, configOptions));
        return this;
    }

    /// <summary>
    /// Aggiunge una regola con descrizione e categorie.
    /// </summary>
    public RegexRuleBuilder Add(string from, string to, string description,
        List<string> categories = null, ConfigOptions configOptions = ConfigOptions.NonUscireInCasoDiMatch)
    {
        var regexDescription = new RegexTransformationRule(from, to, description, categories ?? new List<string>(), configOptions);
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

    public List<RegexTransformationRule> Build()
    {
        return _rules;
    }

    public IEnumerator<RegexTransformationRule> GetEnumerator()
    {
        return _rules.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void AddRange(List<RegexTransformationRule> regexDescriptions)
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

### Contenuto di RegexTransformationRule.cs ###
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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
        public List<TransformationDebugData> DebugData { get; } = new();

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
        public (TransformationDebugData res, Description description) Apply(Description description)
        {
            // Si appoggia all'extension method in RegexHelper
            return this.ApplyReplacement(description);
        }

        /// <summary>
        /// Applica la regola a una stringa di input e restituisce il DebugData.
        /// </summary>
        public TransformationDebugData Apply(string input)
        {
            return this.ApplyReplacement(input);
        }

        /// <summary>
        /// Simula l'applicazione senza modificare la Description.
        /// </summary>
        public TransformationDebugData Simulate(string input)
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

### Contenuto di SentryTracingAspectAttribute.cs ###
using System.Diagnostics;
using PostSharp.Aspects;
using PostSharp.Serialization;
using Sentry;

namespace RegexNodeGraph;

[PSerializable]
public class SentryTracingAspect : OnMethodBoundaryAspect
{
    public override void OnEntry(MethodExecutionArgs args)
    {
        // Avvia una transazione di tracing con il nome del metodo
        var transaction = SentrySdk.StartTransaction(args.Method.DeclaringType.FullName + "." + args.Method.Name, "method-execution");
        args.MethodExecutionTag = transaction;

        Debug.WriteLine($"[Sentry] Start tracing: {args.Method.DeclaringType.FullName}.{args.Method.Name}");
    }

    public override void OnExit(MethodExecutionArgs args)
    {
        if (args.MethodExecutionTag is ISpan transaction)
        {
            transaction.Finish(SpanStatus.Ok);
        }

        Debug.WriteLine($"[Sentry] Finished tracing: {args.Method.DeclaringType.FullName}.{args.Method.Name}");
    }

    public override void OnException(MethodExecutionArgs args)
    {
        if (args.MethodExecutionTag is ISpan transaction)
        {
            transaction.Finish(SpanStatus.InternalError);
        }

        SentrySdk.CaptureException(args.Exception);

        Debug.WriteLine($"[Sentry] Error in: {args.Method.DeclaringType.FullName}.{args.Method.Name}: {args.Exception.Message}");
    }
}

