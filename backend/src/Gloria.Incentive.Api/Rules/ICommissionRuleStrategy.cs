using Gloria.Incentive.Api.Domain;

namespace Gloria.Incentive.Api.Rules;

public interface ICommissionRuleStrategy
{
    RuleType Type { get; }
    string Label { get; }
    string ParameterHint { get; }
    void ValidateParameters(string parametersJson);
    RuleOutcome Evaluate(RuleContext context);
}
