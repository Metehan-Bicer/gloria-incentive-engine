using Gloria.Incentive.Api.Domain;

namespace Gloria.Incentive.Api.Calculation;

public record CalculationLineDto(
    int Sequence,
    CalculationLineType LineType,
    int? SaleRecordId,
    int? RuleId,
    string? RuleName,
    decimal BaseAmount,
    decimal? Rate,
    decimal Amount,
    string Description,
    SaleInfoDto? Sale);

public record SaleInfoDto(
    SourceSystem SourceSystem,
    string ExternalDocumentNo,
    DateOnly TransactionDate,
    string ProductCode,
    string ProductName,
    string ProductCategory,
    decimal Amount,
    bool IsRefund);

public record RuleSummaryDto(int RuleId, string RuleName, RuleType RuleType, int TransactionCount, decimal BaseAmount, decimal Amount);

public record CommissionResultDto(
    int CalculationId,
    string EmployeeNo,
    string FullName,
    string Department,
    string HotelCode,
    int Year,
    int Month,
    decimal GrossSales,
    decimal RefundTotal,
    decimal NetSales,
    decimal TotalCommission,
    DateTime CalculatedAt,
    string CalculatedBy,
    bool IsFinal,
    bool PeriodClosed,
    IReadOnlyList<RuleSummaryDto> RuleSummaries,
    IReadOnlyList<CalculationLineDto> Lines);

public record EmployeeCommissionSummaryDto(
    string EmployeeNo,
    string FullName,
    string Department,
    string HotelCode,
    decimal GrossSales,
    decimal RefundTotal,
    decimal TotalCommission,
    DateTime? CalculatedAt,
    bool IsFinal);

public record PeriodSummaryDto(
    int Year,
    int Month,
    PeriodStatus Status,
    DateTime? ClosedAt,
    string? ClosedBy,
    int EmployeeCount,
    decimal TotalCommission,
    IReadOnlyList<EmployeeCommissionSummaryDto> Employees);
