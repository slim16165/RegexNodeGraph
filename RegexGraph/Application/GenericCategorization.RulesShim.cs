using RegexNodeGraph.RegexRules;

namespace RegexNodeGraph.Application;

public partial class GenericCategorization
{
    /// <summary>
    /// Espone di nuovo il builder, così il vecchio ViewModel compila.
    /// </summary>
    public RegexRuleBuilder Rules
    {
        get => _rules;
        set => _rules = value;
    }
}
