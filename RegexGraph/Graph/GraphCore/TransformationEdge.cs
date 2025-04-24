using System.Collections.Generic;
using RegexNodeGraph.Model;

namespace RegexNodeGraph.Graph.GraphCore;

public class TransformationEdge : GraphEdge
{
    public ITransformationRule Rule { get; init; }
    public int TransformationCount { get; init; }
    public List<TransformationDebugData> DebugData { get; set; } = new();
}