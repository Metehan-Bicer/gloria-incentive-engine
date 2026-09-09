using Gloria.Incentive.Api.Rules.Strategies;

namespace Gloria.Incentive.Api.Rules;

public static class RuleServiceCollectionExtensions
{
    public static IServiceCollection AddCommissionRules(this IServiceCollection services)
    {
        services.AddSingleton<ICommissionRuleStrategy, FixedPercentageStrategy>();
        services.AddSingleton<ICommissionRuleStrategy, TieredRateStrategy>();
        services.AddSingleton<ICommissionRuleStrategy, FixedAmountPerTransactionStrategy>();
        services.AddSingleton<RuleStrategyResolver>();
        return services;
    }
}
