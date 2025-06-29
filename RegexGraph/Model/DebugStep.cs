namespace RegexNodeGraph.Model;

/// <summary>
/// Rappresenta un singolo passo della pipeline di trasformazione
/// per una descrizione di transazione.
/// </summary>
public sealed record DebugStep(
    string StepLabel,      // “1. Pulizia date”
    string Regex,          // pattern applicato
    string Output,         // descrizione risultante allo step
    string? Category);     // categoria assegnata (null se non cambia)
