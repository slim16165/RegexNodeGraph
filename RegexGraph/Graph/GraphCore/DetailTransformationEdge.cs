using RegexNodeGraph.Model;

namespace RegexNodeGraph.Graph.GraphCore;

public class DetailTransformationEdge : GraphEdge
{
    public TransformationDebugData DebugData { get; set; }
    // Trasformazioni a livello di singola transazione
}