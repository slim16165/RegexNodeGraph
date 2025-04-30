using RegexNodeGraph.Model;

namespace RegexNodeGraph.Graph.GraphCore;

public abstract class GraphEdge
{
    public GraphNode SourceNode { get; set; }
    public GraphNode TargetNode { get; set; }
    public ITransformationRule Rule { get; set; }
}