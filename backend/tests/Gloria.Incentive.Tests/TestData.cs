using Gloria.Incentive.Api.Domain;

namespace Gloria.Incentive.Tests;

internal static class TestData
{
    private static int _nextSaleId = 1;

    public static Department Spa { get; } = new() { Id = 1, Name = "SPA" };
    public static Department FoodAndBeverage { get; } = new() { Id = 2, Name = "F&B" };

    public static Employee Employee(string no = "P1001", Department? department = null, DateOnly? hireDate = null, DateOnly? terminationDate = null)
    {
        department ??= Spa;
        return new Employee
        {
            Id = int.Parse(no[1..]),
            EmployeeNo = no,
            FullName = "Test " + no,
            Department = department,
            DepartmentId = department.Id,
            HotelCode = "GSR",
            HireDate = hireDate ?? new DateOnly(2024, 1, 1),
            TerminationDate = terminationDate
        };
    }

    public static SaleRecord Sale(Employee employee, decimal amount, string productCode = "SPA_MSJ60", string category = "SPA",
        bool refund = false, SourceSystem source = SourceSystem.PMS, DateOnly? date = null)
    {
        return new SaleRecord
        {
            Id = _nextSaleId++,
            SourceSystem = source,
            ExternalDocumentNo = "D" + _nextSaleId,
            TransactionDate = date ?? new DateOnly(2026, 8, 10),
            ProductCode = productCode,
            ProductName = productCode,
            ProductCategory = category,
            Quantity = 1,
            Amount = amount,
            Currency = "TRY",
            IsRefund = refund,
            Employee = employee,
            EmployeeId = employee.Id,
            HotelCode = employee.HotelCode
        };
    }

    public static CommissionRule Percentage(decimal percentage, string? category = "SPA", int id = 1, SourceSystem? source = null, int? departmentId = null)
        => new()
        {
            Id = id,
            Name = $"%{percentage}",
            RuleType = RuleType.FixedPercentage,
            ParametersJson = $$"""{"percentage":{{percentage}}}""",
            ProductCategory = category,
            SourceSystem = source,
            DepartmentId = departmentId,
            Priority = id,
            ValidFrom = new DateOnly(2026, 1, 1),
            IsActive = true
        };

    public static CommissionRule FixedAmount(decimal amount, string? category = "BUGGY", int id = 2)
        => new()
        {
            Id = id,
            Name = $"{amount} TL",
            RuleType = RuleType.FixedAmountPerTransaction,
            ParametersJson = $$"""{"amount":{{amount}}}""",
            ProductCategory = category,
            Priority = id,
            ValidFrom = new DateOnly(2026, 1, 1),
            IsActive = true
        };

    public static CommissionRule Tiered(string parametersJson, string? category = "ALC", int id = 3)
        => new()
        {
            Id = id,
            Name = "Kademeli",
            RuleType = RuleType.TieredRate,
            ParametersJson = parametersJson,
            ProductCategory = category,
            Priority = id,
            ValidFrom = new DateOnly(2026, 1, 1),
            IsActive = true
        };

    public const string DefaultTiers = """{"mode":"Marginal","tiers":[{"threshold":0,"rate":2},{"threshold":30000,"rate":4},{"threshold":60000,"rate":6}]}""";
}
