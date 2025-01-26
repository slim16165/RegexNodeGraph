namespace RegexNodeGraph.Runtime.Graph.Model;

public class DetailTransformationEdge : GraphEdge
{
    public RegexDescription RegexRule { get; set; }
    public RegexDebugData DebugData { get; set; }
    // Trasformazioni a livello di singola transazione
}