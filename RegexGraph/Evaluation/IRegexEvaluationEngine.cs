using System.Threading;
using System.Threading.Tasks;
using RegexNodeGraph.RegexRules;

namespace RegexNodeGraph.Evaluation;

public interface IRegexEvaluationEngine
{
    RegexEvaluationReport EvaluateAll(RegexEvaluationRequest request);
    RegexEvaluationReport EvaluateRule(RegexEvaluationRequest request, RegexTransformationRule rule);
    Task<RegexEvaluationReport> EvaluateAllAsync(RegexEvaluationRequest request, CancellationToken ct = default);
    Task<RegexEvaluationReport> EvaluateRuleAsync(RegexEvaluationRequest request, RegexTransformationRule rule, CancellationToken ct = default);
}
