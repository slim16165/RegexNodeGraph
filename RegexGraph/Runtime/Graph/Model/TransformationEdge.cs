using System.Collections.Generic;

namespace RegexNodeGraph.Runtime.Graph.Model;

public class TransformationEdge : GraphEdge
{
    public RegexDescription RegexRule { get; set; }
    public int TransformationCount { get; set; }
    public List<RegexDebugData> DebugData { get; set; } = new List<RegexDebugData>();
}