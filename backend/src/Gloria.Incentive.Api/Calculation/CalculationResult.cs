using Gloria.Incentive.Api.Domain;

namespace Gloria.Incentive.Api.Calculation;

public class CalculationResult
{
    public decimal GrossSales { get; set; }
    public decimal RefundTotal { get; set; }
    public decimal TotalCommission { get; set; }
    public List<CalculationLine> Lines { get; } = new();
}

public class CalculationLine
{
    public int Sequence { get; set; }
    public CalculationLineType LineType { get; set; }
    public int? SaleRecordId { get; set; }
    public int? RuleId { get; set; }
    public string? RuleName { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal? Rate { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
}
