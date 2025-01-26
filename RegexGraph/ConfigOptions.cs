using System;

namespace RegexNodeGraph;

[Flags]
public enum ConfigOptions
{
    NonUscireInCasoDiMatch = 1,
    EsciInCasoDiMatch = 2,
    IgnoraRetry = 4
}