namespace RegexNodeGraph.Graph.GraphCore;

public record TransformationResult(
    string Input,
    string Output,
    bool IsMatch,
    long ElapsedMs);