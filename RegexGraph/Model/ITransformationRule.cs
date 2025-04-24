using System.Collections.Generic;

namespace RegexNodeGraph.Model;

public interface ITransformationRule
{
    (TransformationDebugData res, Description description) Apply(Description description);
    TransformationDebugData Apply(string input);
    TransformationDebugData Simulate(string input);
}

public interface IRuleMetadata
{
    string From { get; }
    string To { get; }
    string Description { get; }
    List<string> Categories { get; }

    string ToDebugString();
}