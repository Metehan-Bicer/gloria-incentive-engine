namespace Gloria.Incentive.Api.Domain;

public class CommissionCalculation
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal GrossSales { get; set; }
    public decimal RefundTotal { get; set; }
    public decimal TotalCommission { get; set; }
    public DateTime CalculatedAt { get; set; }
    public string CalculatedBy { get; set; } = string.Empty;
    public bool IsFinal { get; set; }

    public ICollection<CommissionCalculationLine> Lines { get; set; } = new List<CommissionCalculationLine>();
}

public class CommissionCalculationLine
{
    public int Id { get; set; }
    public int CalculationId { get; set; }
    public CommissionCalculation Calculation { get; set; } = null!;
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
