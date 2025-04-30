using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Neo4j.Driver;
using RegexNodeGraph.Graph.GraphCore;
using RegexNodeGraph.Model;
using RegexNodeGraph.Graph.Processing;

namespace RegexNodeGraph.Graph.Processing
{
    /// <summary>
    /// "Bolt" builder: raccoglie le operazioni e le esegue su Neo4j.
    /// Implementa IGraphBuilder per costruire live il grafo.
    /// </summary>
    internal class GraphBuilderNeo4j : IGraphBuilder, IDisposable
    {
        private readonly IDriver _driver;
        private readonly List<Func<IAsyncTransaction, Task>> _operations = new();

        public GraphBuilderNeo4j(IDriver driver)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        public void Reset()
        {
            _operations.Clear();
        }

        public void AddNode(AggregatedTransactionsNode node)
        {
            _operations.Add(async tx =>
            {
                const string cypher = @"
                    CREATE (n:AggregatedNode {id: $id, descriptions_count: $count})";
                var parameters = new Dictionary<string, object>
                {
                    { "id", node.Id },
                    { "count", node.Cardinality }
                };
                await tx.RunAsync(cypher, parameters);
            });
        }

        public void AddNode(DetailedTransactionNode node)
        {
            _operations.Add(async tx =>
            {
                const string cypher = @"
                    CREATE (n:DetailedNode {id: $id, description: $descr})";
                var parameters = new Dictionary<string, object>
                {
                    { "id", node.Id },
                    { "descr", node.Description.CurrentDescription }
                };
                await tx.RunAsync(cypher, parameters);
            });
        }

        public void AddMembershipEdge(DetailedTransactionNode src, AggregatedTransactionsNode dst)
        {
            _operations.Add(async tx =>
            {
                const string cypher = @"
                    MATCH (f:DetailedNode {id: $src}), (t:AggregatedNode {id: $tgt})
                    CREATE (f)-[:BELONGS_TO]->(t)";
                var parameters = new Dictionary<string, object>
                {
                    { "src", src.Id },
                    { "tgt", dst.Id }
                };
                await tx.RunAsync(cypher, parameters);
            });
        }

        public void AddDetailEdge(DetailedTransactionNode src,
                                  DetailedTransactionNode dst,
                                  TransformationRuleBase rule)
        {
            _operations.Add(async tx =>
            {
                string rx = (rule as IRegexRuleMetadata)?.From ?? string.Empty;
                const string cypher = @"
                    MATCH (f:DetailedNode {id: $src}), (t:DetailedNode {id: $tgt})
                    CREATE (f)-[:DETAIL_TRANSFORMS {regex: $rx}]->(t)";
                var parameters = new Dictionary<string, object>
                {
                    { "src", src.Id },
                    { "tgt", dst.Id },
                    { "rx", rx }
                };
                await tx.RunAsync(cypher, parameters);
            });
        }

        public void AddAggregatedEdge(AggregatedTransactionsNode src,
                                      AggregatedTransactionsNode dst,
                                      TransformationRuleBase rule,
                                      int transformationCount,
                                      IReadOnlyList<TransformationDebugData> debug)
        {
            _operations.Add(async tx =>
            {
                string rx = (rule as IRegexRuleMetadata)?.From ?? string.Empty;
                const string cypher = @"
                    MATCH (f:AggregatedNode {id: $src}), (t:AggregatedNode {id: $tgt})
                    CREATE (f)-[:AGGREGATED_TRANSFORMS {regex: $rx, count: $count}]->(t)";
                var parameters = new Dictionary<string, object>
                {
                    { "src", src.Id },
                    { "tgt", dst.Id },
                    { "rx", rx },
                    { "count", transformationCount }
                };
                await tx.RunAsync(cypher, parameters);
            });
        }

        // Metodi aggiuntivi per BankTransaction, se ancora necessari:

        public void AddDetailNode_BankTransaction(string description)
        {
            _operations.Add(async tx =>
            {
                const string cypher = @"
                    MERGE (:BankTransaction {description: $descr})";
                var parameters = new Dictionary<string, object>
                {
                    { "descr", description }
                };
                await tx.RunAsync(cypher, parameters);
            });
        }

        public void AddDetailEdge(string originalTransaction,
                                  string transformedTransaction,
                                  ITransformationRule rule)
        {
            _operations.Add(async tx =>
            {
                string rx = (rule as IRegexRuleMetadata)?.From ?? string.Empty;
                const string cypher = @"
                    MATCH (n1:BankTransaction {description: $orig}), (n2:BankTransaction {description: $trans})
                    CREATE (n1)-[:TRANSFORMS {regex: $rx}]->(n2)";
                var parameters = new Dictionary<string, object>
                {
                    { "orig", originalTransaction },
                    { "trans", transformedTransaction },
                    { "rx", rx }
                };
                await tx.RunAsync(cypher, parameters);
            });
        }

        /// <summary>
        /// Esegue tutte le operazioni in una singola transazione di scrittura.
        /// </summary>
        public async Task ExecuteAsync(int batchSize = 1000)
        {
            await using var session = _driver.AsyncSession();
            try
            {
                for (int i = 0; i < _operations.Count; i += batchSize)
                {
                    var slice = _operations.GetRange(i, Math.Min(batchSize, _operations.Count - i));
                    await session.WriteTransactionAsync(async tx =>
                    {
                        foreach (var op in slice)
                            await op(tx);
                    });
                }
            }
            finally
            {
                await session.CloseAsync();
            }
        }

        /// <summary>
        /// Non usato per Bolt, restituisce uno script di debug se necessario.
        /// </summary>
        public string Build() => string.Empty;

        public void Dispose()
        {
            _driver?.Dispose();
        }
    }
}
