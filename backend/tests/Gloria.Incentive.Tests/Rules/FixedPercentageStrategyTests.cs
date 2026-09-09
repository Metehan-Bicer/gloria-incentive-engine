using Gloria.Incentive.Api.Domain;
using Gloria.Incentive.Api.Rules;
using Gloria.Incentive.Api.Rules.Strategies;

namespace Gloria.Incentive.Tests.Rules;

public class FixedPercentageStrategyTests
{
    private readonly FixedPercentageStrategy _strategy = new();
    private readonly Employee _employee = TestData.Employee();

    [Fact]
    public void Applies_percentage_to_each_sale()
    {
        var rule = TestData.Percentage(5);
        var outcome = _strategy.Evaluate(new RuleContext
        {
            Rule = rule,
            Employee = _employee,
            Sales = [TestData.Sale(_employee, 2800), TestData.Sale(_employee, 3900)],
            Year = 2026,
            Month = 8
        });

        Assert.Equal(335m, outcome.Subtotal);
        Assert.All(outcome.Steps, s => Assert.Equal(5m, s.Rate));
    }

    [Fact]
    public void Refund_is_deducted_with_negative_amount()
    {
        var rule = TestData.Percentage(5);
        var outcome = _strategy.Evaluate(new RuleContext
        {
            Rule = rule,
            Employee = _employee,
            Sales = [TestData.Sale(_employee, 3900), TestData.Sale(_employee, 3900, refund: true)],
            Year = 2026,
            Month = 8
        });

        Assert.Equal(0m, outcome.Subtotal);
        var refund = Assert.Single(outcome.Steps, s => s.LineType == CalculationLineType.Refund);
        Assert.Equal(-3900m, refund.BaseAmount);
        Assert.Equal(-195m, refund.Amount);
    }

    [Theory]
    [InlineData("""{"percentage":0}""")]
    [InlineData("""{"percentage":120}""")]
    [InlineData("{}")]
    public void Invalid_parameters_are_rejected(string json)
    {
        Assert.Throws<RuleParameterException>(() => _strategy.ValidateParameters(json));
    }
}
