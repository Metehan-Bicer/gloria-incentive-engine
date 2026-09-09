using Gloria.Incentive.Api.Calculation;
using Gloria.Incentive.Api.Domain;
using Gloria.Incentive.Api.Rules;
using Gloria.Incentive.Api.Rules.Strategies;

namespace Gloria.Incentive.Tests.Calculation;

public class CommissionCalculatorTests
{
    private readonly CommissionCalculator _calculator = new(new RuleStrategyResolver(
        [new FixedPercentageStrategy(), new TieredRateStrategy(), new FixedAmountPerTransactionStrategy()]));

    [Fact]
    public void Combines_multiple_rules_and_reports_subtotals_and_total()
    {
        var employee = TestData.Employee();
        var sales = new[]
        {
            TestData.Sale(employee, 2800),
            TestData.Sale(employee, 2500, "BUG_4H", "BUGGY"),
            TestData.Sale(employee, 45700, "ALC_ITL", "ALC")
        };
        var rules = new[] { TestData.Percentage(5), TestData.FixedAmount(50), TestData.Tiered(TestData.DefaultTiers) };

        var result = _calculator.Calculate(employee, sales, rules, 2026, 8);

        Assert.Equal(51000m, result.GrossSales);
        Assert.Equal(140m + 50m + 1228m, result.TotalCommission);
        Assert.Equal(3, result.Lines.Count(l => l.LineType == CalculationLineType.RuleSubtotal));
        Assert.Equal(CalculationLineType.Total, result.Lines.Last().LineType);
        Assert.Equal(result.Lines.Count, result.Lines.Last().Sequence);
    }

    [Fact]
    public void Refunds_reduce_gross_and_commission()
    {
        var employee = TestData.Employee();
        var sales = new[]
        {
            TestData.Sale(employee, 3900),
            TestData.Sale(employee, 11700),
            TestData.Sale(employee, 11700, refund: true)
        };

        var result = _calculator.Calculate(employee, sales, [TestData.Percentage(5)], 2026, 8);

        Assert.Equal(15600m, result.GrossSales);
        Assert.Equal(11700m, result.RefundTotal);
        Assert.Equal(195m, result.TotalCommission);
    }

    [Fact]
    public void Total_never_goes_below_zero_when_refunds_exceed_sales()
    {
        var employee = TestData.Employee();
        var sales = new[] { TestData.Sale(employee, 1000), TestData.Sale(employee, 5000, refund: true) };

        var result = _calculator.Calculate(employee, sales, [TestData.Percentage(5)], 2026, 8);

        Assert.Equal(0m, result.TotalCommission);
        Assert.Equal(-200m, result.Lines.Single(l => l.LineType == CalculationLineType.RuleSubtotal).Amount);
    }

    [Fact]
    public void Sales_outside_employment_window_are_excluded_with_reason()
    {
        var employee = TestData.Employee(hireDate: new DateOnly(2026, 8, 18), terminationDate: new DateOnly(2026, 8, 25));
        var sales = new[]
        {
            TestData.Sale(employee, 1000, date: new DateOnly(2026, 8, 10)),
            TestData.Sale(employee, 2000, date: new DateOnly(2026, 8, 20)),
            TestData.Sale(employee, 3000, date: new DateOnly(2026, 8, 28))
        };

        var result = _calculator.Calculate(employee, sales, [TestData.Percentage(10)], 2026, 8);

        Assert.Equal(200m, result.TotalCommission);
        Assert.Equal(2000m, result.GrossSales);
        var excluded = result.Lines.Where(l => l.LineType == CalculationLineType.Excluded).ToList();
        Assert.Equal(2, excluded.Count);
        Assert.Contains(excluded, l => l.Description.Contains("işe başlama"));
        Assert.Contains(excluded, l => l.Description.Contains("ayrılış"));
    }

    [Fact]
    public void Sales_without_matching_rule_are_listed_as_excluded()
    {
        var employee = TestData.Employee();
        var sales = new[] { TestData.Sale(employee, 3500, "GLF_LSN", "GOLF") };

        var result = _calculator.Calculate(employee, sales, [TestData.Percentage(5)], 2026, 8);

        Assert.Equal(0m, result.TotalCommission);
        var excluded = Assert.Single(result.Lines, l => l.LineType == CalculationLineType.Excluded);
        Assert.Contains("uygun prim kuralı yok", excluded.Description);
    }

    [Fact]
    public void Rule_scope_filters_by_source_and_department()
    {
        var spaEmployee = TestData.Employee("P1001", TestData.Spa);
        var fbEmployee = TestData.Employee("P1004", TestData.FoodAndBeverage);

        var posOnly = TestData.Percentage(4, "SPA_RETAIL", id: 1, source: SourceSystem.POS);
        var fbOnly = TestData.Percentage(2, category: null, id: 2, departmentId: TestData.FoodAndBeverage.Id);

        var spaResult = _calculator.Calculate(spaEmployee,
            [TestData.Sale(spaEmployee, 1000, "5001", "SPA_RETAIL", source: SourceSystem.POS), TestData.Sale(spaEmployee, 1000, "5001", "SPA_RETAIL", source: SourceSystem.PMS)],
            [posOnly, fbOnly], 2026, 8);

        var fbResult = _calculator.Calculate(fbEmployee,
            [TestData.Sale(fbEmployee, 1000, "ALC_ITL", "ALC")],
            [posOnly, fbOnly], 2026, 8);

        Assert.Equal(40m, spaResult.TotalCommission);
        Assert.Equal(20m, fbResult.TotalCommission);
    }

    [Fact]
    public void Inactive_or_expired_rules_are_ignored()
    {
        var employee = TestData.Employee();
        var expired = TestData.Percentage(5);
        expired.ValidTo = new DateOnly(2026, 7, 31);
        var inactive = TestData.Percentage(5, id: 9);
        inactive.IsActive = false;

        var result = _calculator.Calculate(employee, [TestData.Sale(employee, 1000)], [expired, inactive], 2026, 8);

        Assert.Equal(0m, result.TotalCommission);
    }

    [Fact]
    public void New_rule_from_database_row_is_applied_without_code_changes()
    {
        var employee = TestData.Employee();
        var sales = new[] { TestData.Sale(employee, 3500, "GLF_LSN", "GOLF") };

        var golfRule = new CommissionRule
        {
            Id = 42,
            Name = "Golf dersi %7",
            RuleType = RuleType.FixedPercentage,
            ParametersJson = """{"percentage":7}""",
            ProductCategory = "GOLF",
            ValidFrom = new DateOnly(2026, 1, 1),
            IsActive = true
        };

        var result = _calculator.Calculate(employee, sales, [golfRule], 2026, 8);

        Assert.Equal(245m, result.TotalCommission);
    }
}
