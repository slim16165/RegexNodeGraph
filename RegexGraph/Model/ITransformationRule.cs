namespace RegexNodeGraph.Model;

public interface ITransformationRule
{
    (TransformationDebugData res, Description description) Apply(Description description);
    TransformationDebugData Apply(string input);
    TransformationDebugData Simulate(string input);
}