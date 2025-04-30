using System.Collections.Generic;
using RegexNodeGraph.Graph.GraphCore;
using RegexNodeGraph.Model;

namespace RegexNodeGraph.Graph.Processing;

public interface IGraphBuilder
{
    /* nodi ------------------------------------------------ */
    void AddNode(AggregatedTransactionsNode node);
    void AddNode(DetailedTransactionNode node);

    /* archi ----------------------------------------------- */
    void AddMembershipEdge(DetailedTransactionNode src,
        AggregatedTransactionsNode dst);

    void AddDetailEdge(DetailedTransactionNode src,
        DetailedTransactionNode dst,
        TransformationRuleBase rule);

    void AddAggregatedEdge(AggregatedTransactionsNode src,
        AggregatedTransactionsNode dst,
        TransformationRuleBase rule,
        int transformationCount,
        IReadOnlyList<TransformationDebugData> debug);

    /* output / lifecycle ---------------------------------- */
    void Reset();
    /// Tipico per un builder testuale:
    string Build();              // restituisce lo script
}