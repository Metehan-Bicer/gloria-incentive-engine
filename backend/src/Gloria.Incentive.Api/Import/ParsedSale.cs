namespace Gloria.Incentive.Api.Import;

public class ParsedSale
{
    public string ExternalDocumentNo { get; init; } = string.Empty;
    public DateOnly TransactionDate { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public string ProductName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = "TRY";
    public bool IsRefund { get; init; }
    public string? RefundReference { get; init; }
    public string EmployeeNo { get; init; } = string.Empty;
    public string? HotelCode { get; init; }
}

public class RowParseException : Exception
{
    public RowParseException(string message) : base(message)
    {
    }
}
