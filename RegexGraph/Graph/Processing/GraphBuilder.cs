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