namespace RegexNodeGraph.Model;

/// <summary>
/// Istanza atomica di trasformazione ottenuta dal motore.
/// Source e Target possono coincidere se la regola non ha prodotto output
/// (il record serve comunque per il debug/count).
/// </summary>
public sealed record TransformationRecord(
    Description Source,
    Description Target,
    ITransformationRule Rule,
    TransformationDebugData Debug);