using Gloria.Incentive.Api.Domain;

namespace Gloria.Incentive.Api.Rules;

public class RuleContext
{
    public required CommissionRule Rule { get; init; }
    public required Employee Employee { get; init; }
    public required IReadOnlyList<SaleRecord> Sales { get; init; }
    public required int Year { get; init; }
    public required int Month { get; init; }
}

public class RuleStep
{
    public CalculationLineType LineType { get; init; }
    public int? SaleRecordId { get; init; }
    public decimal BaseAmount { get; init; }
    public decimal? Rate { get; init; }
    public decimal Amount { get; init; }
    public string Description { get; init; } = string.Empty;
}

public class RuleOutcome
{
    public List<RuleStep> Steps { get; } = new();
    public decimal Subtotal { get; set; }
}
