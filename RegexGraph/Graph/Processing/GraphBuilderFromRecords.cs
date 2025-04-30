using System.Collections.Generic;
using System.Linq;
using RegexNodeGraph.Graph.GraphCore;
using RegexNodeGraph.Model;

namespace RegexNodeGraph.Graph.Processing;

/// <summary>
/// Traduce i <see cref="TransformationRecord"/> emessi dal motore in un
/// <see cref="TransformationGraph"/> identico – per semantica – a quello
/// prodotto in precedenza.
/// </summary>
public sealed class GraphBuilderFromRecords
{
    /* -------------------------------------------------------------
     *  Accumulatori interni
     * ----------------------------------------------------------- */
    private readonly Dictionary<Description, DetailedTransactionNode> _detail = new();
    private readonly Dictionary<Description, AggregatedTransactionsNode> _agg = new();
    private readonly Dictionary<(int srcId, int dstId, ITransformationRule rule),
                                 TransformationEdge> _aggEdges = new();

    private readonly List<GraphNode> _nodes = new();
    private readonly List<GraphEdge> _edges = new();
    private int _id;

    /* -------------------------------------------------------------
     *  API
     * ----------------------------------------------------------- */

    public (IReadOnlyList<GraphNode> nodes,
            IReadOnlyList<GraphEdge> edges) Build(IEnumerable<TransformationRecord> records)
    {
        foreach (var r in records)
            AddRecord(r);

        return (_nodes, _edges);
    }

    /* -------------------------------------------------------------
     *  Record → nodi / archi
     * ----------------------------------------------------------- */

    private void AddRecord(TransformationRecord rec)
    {
        /* 1) detail nodes + membership */
        var srcDetail = GetOrAddDetail(rec.Source);
        var dstDetail = GetOrAddDetail(rec.Target);

        /* 2) aggregated nodes (uno per “descrizione corrente”) */
        var srcAgg = GetOrAddAggregated(rec.Source);
        var dstAgg = GetOrAddAggregated(rec.Target);

        /* 3) MembershipEdge: dettaglio → aggregato */
        LinkMembership(srcDetail, srcAgg);
        LinkMembership(dstDetail, dstAgg);

        /* 4) DetailTransformationEdge (sempre, anche se Source == Target) */
        _edges.Add(new DetailTransformationEdge
        {
            SourceNode = srcDetail,
            TargetNode = dstDetail,
            Rule = rec.Rule,
            DebugData = rec.Debug
        });

        /* 5) Aggregated edge: raggruppiamo per (srcAgg, dstAgg, rule) */
        AddAggregatedEdge(srcAgg, dstAgg, rec);
    }

    /* -------------------------------------------------------------
     *  Helpers – det/agg nodes
     * ----------------------------------------------------------- */

    private DetailedTransactionNode GetOrAddDetail(Description d)
    {
        if (_detail.TryGetValue(d, out var n)) return n;

        n = new DetailedTransactionNode { Id = _id++, Description = d };
        _detail[d] = n;
        _nodes.Add(n);
        return n;
    }

    private AggregatedTransactionsNode GetOrAddAggregated(Description d)
    {
        // chiave: la stessa Description (reference) -> mantiene la
        // compatibilità 1-a-1 con il vecchio algoritmo
        if (_agg.TryGetValue(d, out var n)) return n;

        n = new AggregatedTransactionsNode
        {
            Id = _id++,
            Descriptions = new List<Description> { d }
        };



        _agg[d] = n;
        _nodes.Add(n);
        return n;
    }

    /* -------------------------------------------------------------
     *  Helpers – archi
     * ----------------------------------------------------------- */

    private void LinkMembership(DetailedTransactionNode det,
                                AggregatedTransactionsNode agg)
    {
        // Evitiamo duplicati
        if (_edges.Any(e => e is MembershipEdge
                         && e.SourceNode == det
                         && e.TargetNode == agg))
            return;

        _edges.Add(new MembershipEdge
        {
            SourceNode = det,
            TargetNode = agg
        });
    }

    private void AddAggregatedEdge(AggregatedTransactionsNode srcAgg,
                                   AggregatedTransactionsNode dstAgg,
                                   TransformationRecord rec)
    {
        // self-loop degli aggregati? il vecchio codice li saltava
        if (srcAgg == dstAgg) return;

        var key = (srcAgg.Id, dstAgg.Id, rec.Rule);
        if (!_aggEdges.TryGetValue(key, out var edge))
        {
            edge = new TransformationEdge
            {
                SourceNode = srcAgg,
                TargetNode = dstAgg,
                Rule = rec.Rule,
                TransformationCount = 0
            };
            _aggEdges[key] = edge;
            _edges.Add(edge);
        }

        edge.TransformationCount += 1;
        edge.DebugData.Add(rec.Debug);
    }
}
