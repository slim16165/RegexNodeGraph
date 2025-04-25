using System.Collections.Generic;
using RegexNodeGraph.Graph.GraphCore;

namespace RegexNodeGraph.Graph.Processing;

/// <summary>
/// Non calcola più nulla: è solo un DTO che contiene nodi e archi
/// prodotti altrove (es. <see cref="GraphBuilderFromRecords"/>).
/// </summary>
public sealed partial class TransformationGraph
{
    public List<GraphNode> Nodes { get; init; } = new();
    public List<GraphEdge> Edges { get; init; } = new();
}

