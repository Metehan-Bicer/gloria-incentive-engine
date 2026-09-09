using Gloria.Incentive.Api.Domain;
using Gloria.Incentive.Api.Rules;
using Gloria.Incentive.Api.Rules.Strategies;

namespace Gloria.Incentive.Tests.Rules;

public class TieredRateStrategyTests
{
    private readonly TieredRateStrategy _strategy = new();
    private readonly Employee _employee = TestData.Employee();

    private RuleOutcome Evaluate(CommissionRule rule, params SaleRecord[] sales)
        => _strategy.Evaluate(new RuleContext { Rule = rule, Employee = _employee, Sales = sales, Year = 2026, Month = 8 });

    [Fact]
    public void Net_total_below_first_threshold_uses_only_first_tier()
    {
        var rule = TestData.Tiered(TestData.DefaultTiers);
        var outcome = Evaluate(rule, TestData.Sale(_employee, 10000, "ALC_ITL", "ALC"), TestData.Sale(_employee, 5000, "ALC_FSH", "ALC"));

        Assert.Equal(300m, outcome.Subtotal);
        var tiers = outcome.Steps.Where(s => s.LineType == CalculationLineType.Tier).ToList();
        Assert.Single(tiers);
        Assert.Equal(2m, tiers[0].Rate);
        Assert.Equal(15000m, tiers[0].BaseAmount);
    }

    [Fact]
    public void Marginal_mode_splits_amount_across_tiers()
    {
        var rule = TestData.Tiered(TestData.DefaultTiers);
        var outcome = Evaluate(rule, TestData.Sale(_employee, 45700, "ALC_ITL", "ALC"));

        var tiers = outcome.Steps.Where(s => s.LineType == CalculationLineType.Tier).ToList();
        Assert.Equal(2, tiers.Count);
        Assert.Equal(30000m, tiers[0].BaseAmount);
        Assert.Equal(600m, tiers[0].Amount);
        Assert.Equal(15700m, tiers[1].BaseAmount);
        Assert.Equal(628m, tiers[1].Amount);
        Assert.Equal(1228m, outcome.Subtotal);
    }

    [Fact]
    public void Marginal_mode_reaches_top_tier_when_total_exceeds_last_threshold()
    {
        var rule = TestData.Tiered(TestData.DefaultTiers);
        var outcome = Evaluate(rule, TestData.Sale(_employee, 63500, "ALC_ITL", "ALC"));

        var tiers = outcome.Steps.Where(s => s.LineType == CalculationLineType.Tier).ToList();
        Assert.Equal(3, tiers.Count);
        Assert.Equal(3500m, tiers[2].BaseAmount);
        Assert.Equal(6m, tiers[2].Rate);
        Assert.Equal(600m + 1200m + 210m, outcome.Subtotal);
    }

    [Fact]
    public void Highest_mode_applies_reached_tier_rate_to_whole_amount()
    {
        var rule = TestData.Tiered("""{"mode":"Highest","tiers":[{"threshold":0,"rate":2},{"threshold":30000,"rate":4}]}""");
        var outcome = Evaluate(rule, TestData.Sale(_employee, 45000, "ALC_ITL", "ALC"));

        var tier = Assert.Single(outcome.Steps, s => s.LineType == CalculationLineType.Tier);
        Assert.Equal(4m, tier.Rate);
        Assert.Equal(45000m, tier.BaseAmount);
        Assert.Equal(1800m, outcome.Subtotal);
    }

    [Fact]
    public void Refund_reduces_net_total_and_can_drop_to_lower_tier()
    {
        var rule = TestData.Tiered(TestData.DefaultTiers);

        var withoutRefund = Evaluate(rule, TestData.Sale(_employee, 32000, "ALC_ITL", "ALC"));
        var withRefund = Evaluate(rule,
            TestData.Sale(_employee, 32000, "ALC_ITL", "ALC"),
            TestData.Sale(_employee, 5000, "ALC_ITL", "ALC", refund: true));

        Assert.Equal(600m + 80m, withoutRefund.Subtotal);
        Assert.Equal(540m, withRefund.Subtotal);
        Assert.Single(withRefund.Steps, s => s.LineType == CalculationLineType.Tier);
        Assert.Contains(withRefund.Steps, s => s.LineType == CalculationLineType.Refund && s.BaseAmount == -5000m);
    }

    [Fact]
    public void Net_total_at_or_below_zero_yields_no_commission()
    {
        var rule = TestData.Tiered(TestData.DefaultTiers);
        var outcome = Evaluate(rule,
            TestData.Sale(_employee, 1000, "ALC_ITL", "ALC"),
            TestData.Sale(_employee, 1500, "ALC_ITL", "ALC", refund: true));

        Assert.Equal(0m, outcome.Subtotal);
    }

    [Theory]
    [InlineData("""{"tiers":[]}""")]
    [InlineData("""{"tiers":[{"threshold":100,"rate":2}]}""")]
    [InlineData("""{"tiers":[{"threshold":0,"rate":2},{"threshold":0,"rate":3}]}""")]
    [InlineData("""{"tiers":[{"threshold":0,"rate":150}]}""")]
    [InlineData("not json")]
    public void Invalid_parameters_are_rejected(string json)
    {
        Assert.Throws<RuleParameterException>(() => _strategy.ValidateParameters(json));
    }
}
