namespace Gloria.Incentive.Api.Domain;

public class CommissionRule
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public RuleType RuleType { get; set; }
    public string ParametersJson { get; set; } = "{}";
    public string? ProductCategory { get; set; }
    public string? ProductCode { get; set; }
    public SourceSystem? SourceSystem { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
    public int Priority { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public bool IsValidOn(DateOnly date)
    {
        if (!IsActive || date < ValidFrom) return false;
        return ValidTo is null || date <= ValidTo.Value;
    }

    public bool AppliesTo(SaleRecord sale)
    {
        if (ProductCategory is not null && !string.Equals(ProductCategory, sale.ProductCategory, StringComparison.OrdinalIgnoreCase)) return false;
        if (ProductCode is not null && !string.Equals(ProductCode, sale.ProductCode, StringComparison.OrdinalIgnoreCase)) return false;
        if (SourceSystem is not null && SourceSystem != sale.SourceSystem) return false;
        if (DepartmentId is not null && DepartmentId != sale.Employee.DepartmentId) return false;
        return true;
    }
}
