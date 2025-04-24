using System.Collections.Generic;
using RegexNodeGraph.Model;
using RegexNodeGraph.RegexRules;

namespace RegexNodeGraph.Graph.GraphCore;

public interface ITransformationRule
{
    (RegexDebugData res, Description description) Apply(Description description);
    RegexDebugData Apply(string input);
    RegexDebugData Simulate(string input);
}