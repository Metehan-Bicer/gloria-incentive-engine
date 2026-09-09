namespace Gloria.Incentive.Api.Domain;

public class SaleRecord
{
    public int Id { get; set; }
    public SourceSystem SourceSystem { get; set; }
    public string ExternalDocumentNo { get; set; } = string.Empty;
    public DateOnly TransactionDate { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductCategory { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public bool IsRefund { get; set; }
    public string? RefundReference { get; set; }
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    public string HotelCode { get; set; } = string.Empty;
    public int? ImportBatchId { get; set; }
    public ImportBatch? ImportBatch { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public decimal SignedAmount => IsRefund ? -Amount : Amount;
}
