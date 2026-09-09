using Gloria.Incentive.Api.Domain;

namespace Gloria.Incentive.Api.Rules;

public class RuleStrategyResolver
{
    private readonly IReadOnlyDictionary<RuleType, ICommissionRuleStrategy> _strategies;

    public RuleStrategyResolver(IEnumerable<ICommissionRuleStrategy> strategies)
    {
        _strategies = strategies.ToDictionary(s => s.Type);
    }

    public IEnumerable<ICommissionRuleStrategy> All => _strategies.Values.OrderBy(s => s.Type);

    public ICommissionRuleStrategy Resolve(RuleType type)
    {
        if (_strategies.TryGetValue(type, out var strategy))
            return strategy;

        throw new InvalidOperationException($"Kural tipi için strateji tanımlı değil: {type}");
    }
}
