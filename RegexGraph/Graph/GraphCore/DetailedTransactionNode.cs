using RegexNodeGraph.Model;

namespace RegexNodeGraph.Graph.GraphCore;

public class DetailedTransactionNode : GraphNode
{
    public Description Description { get; set; }
    public string Category { get; set; }
}