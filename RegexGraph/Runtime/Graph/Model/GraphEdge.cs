namespace RegexNodeGraph.Runtime.Graph.Model;

public abstract class GraphEdge
{
    public GraphNode SourceNode { get; set; }
    public GraphNode TargetNode { get; set; }
}