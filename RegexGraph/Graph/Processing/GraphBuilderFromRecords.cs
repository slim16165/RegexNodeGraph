using System.Collections.Generic;
using RegexNodeGraph.Graph.GraphCore;
using RegexNodeGraph.Model;

namespace RegexNodeGraph.Graph.Processing;

public sealed class GraphBuilderFromRecords
{
    private readonly Dictionary<Description, DetailedTransactionNode> _detailCache = new();
    private readonly List<GraphNode> _nodes = new();
    private readonly List<GraphEdge> _edges = new();
    private int _id;

    public (IReadOnlyList<GraphNode> nodes, IReadOnlyList<GraphEdge> edges)
        Build(IEnumerable<TransformationRecord> records)
    {
        foreach (var rec in records)
        {
            var srcNode = GetOrAdd(rec.Source);
            var tgtNode = GetOrAdd(rec.Target);

            // Edge di dettaglio
            _edges.Add(new DetailTransformationEdge
            {
                SourceNode = srcNode,
                TargetNode = tgtNode,
                Rule = rec.Rule,
                DebugData = rec.Debug
            });
        }

        return (_nodes, _edges);
    }

    private DetailedTransactionNode GetOrAdd(Description d)
    {
        if (_detailCache.TryGetValue(d, out var node))
            return node;

        node = new DetailedTransactionNode { Id = _id++, Description = d };
        _detailCache[d] = node;
        _nodes.Add(node);
        return node;
    }
}