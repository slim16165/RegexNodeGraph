using System.Collections.Generic;

namespace RegexNodeGraph.Model;

public interface IRegexRuleMetadata
{
    string From { get; }
    string To { get; }
    string Description { get; }
    List<string> Categories { get; }

    string ToDebugString();
}