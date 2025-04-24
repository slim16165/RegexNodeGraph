namespace RegexNodeGraph.Model;

public record TransformationResult(
    string Input,
    string Output,
    bool IsMatch,
    long ElapsedMs);