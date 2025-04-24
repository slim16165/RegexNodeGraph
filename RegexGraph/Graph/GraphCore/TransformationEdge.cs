using System.Collections.Generic;
using RegexNodeGraph.RegexRules;

namespace RegexNodeGraph.Graph.GraphCore;

public class TransformationEdge : GraphEdge
{
    public RegexTransformationRule RegexRule { get; set; }
    public int TransformationCount { get; set; }
    public List<RegexDebugData> DebugData { get; set; } = new List<RegexDebugData>();
}