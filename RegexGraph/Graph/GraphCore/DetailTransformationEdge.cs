using RegexNodeGraph.RegexRules;

namespace RegexNodeGraph.Graph.GraphCore;

public class DetailTransformationEdge : GraphEdge
{
    public RegexTransformationRule RegexRule { get; set; }
    public RegexDebugData DebugData { get; set; }
    // Trasformazioni a livello di singola transazione
}