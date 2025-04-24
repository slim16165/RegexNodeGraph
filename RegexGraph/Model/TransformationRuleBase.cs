namespace RegexNodeGraph.Model;

public abstract class TransformationRuleBase : ITransformationRule
{
    public ConfigOptions ConfigOptions { get; set; }

    public bool EsciInCasoDiMatch => (ConfigOptions & ConfigOptions.EsciInCasoDiMatch) == ConfigOptions.EsciInCasoDiMatch;
    public bool ShouldRetryFromOrig => (ConfigOptions & ConfigOptions.IgnoraRetry) != ConfigOptions.IgnoraRetry;

    public abstract (TransformationDebugData res, Description description) Apply(Description description);
    public abstract TransformationDebugData Apply(string input);
    public abstract TransformationDebugData Simulate(string input);
}