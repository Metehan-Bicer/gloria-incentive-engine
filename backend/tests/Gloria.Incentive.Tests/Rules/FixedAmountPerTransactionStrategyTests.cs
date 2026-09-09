using Gloria.Incentive.Api.Domain;
using Gloria.Incentive.Api.Rules;
using Gloria.Incentive.Api.Rules.Strategies;

namespace Gloria.Incentive.Tests.Rules;

public class FixedAmountPerTransactionStrategyTests
{
    private readonly FixedAmountPerTransactionStrategy _strategy = new();
    private readonly Employee _employee = TestData.Employee();

    [Fact]
    public void Pays_fixed_amount_per_sale_regardless_of_value()
    {
        var rule = TestData.FixedAmount(50);
        var outcome = _strategy.Evaluate(new RuleContext
        {
            Rule = rule,
            Employee = _employee,
            Sales = [TestData.Sale(_employee, 2500, "BUG_4H", "BUGGY"), TestData.Sale(_employee, 13500, "BUG_DAY", "BUGGY")],
            Year = 2026,
            Month = 8
        });

        Assert.Equal(100m, outcome.Subtotal);
    }

    [Fact]
    public void Refunded_transaction_deducts_the_fixed_amount()
    {
        var rule = TestData.FixedAmount(50);
        var outcome = _strategy.Evaluate(new RuleContext
        {
            Rule = rule,
            Employee = _employee,
            Sales =
            [
                TestData.Sale(_employee, 4500, "BUG_DAY", "BUGGY"),
                TestData.Sale(_employee, 4500, "BUG_DAY", "BUGGY"),
                TestData.Sale(_employee, 4500, "BUG_DAY", "BUGGY", refund: true)
            ],
            Year = 2026,
            Month = 8
        });

        Assert.Equal(50m, outcome.Subtotal);
        Assert.Equal(-50m, outcome.Steps.Single(s => s.LineType == CalculationLineType.Refund).Amount);
    }
}
