using System.Collections.Generic;
using RegexNodeGraph.Model;

namespace RegexNodeGraph.Graph.Processing;

public interface ITransformationEngine
{
    IEnumerable<TransformationRecord> Run(
        IEnumerable<Description> inputs,
        IEnumerable<TransformationRuleBase> rules);
}