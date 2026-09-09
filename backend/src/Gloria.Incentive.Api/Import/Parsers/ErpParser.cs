using CsvHelper;
using Gloria.Incentive.Api.Domain;

namespace Gloria.Incentive.Api.Import.Parsers;

public class ErpParser : ISourceParser
{
    public SourceSystem Source => SourceSystem.ERP;

    public IReadOnlyList<string> RequiredHeaders { get; } =
        ["DocNumber", "DocType", "GLDate", "BusinessUnit", "Amount", "Currency", "Reference", "EmployeeNo", "Description", "Status"];

    public ParsedSale Parse(IReaderRow row)
    {
        var documentNo = FieldParsers.Required(row.GetField("DocNumber"), "DocNumber");
        var docType = FieldParsers.Required(row.GetField("DocType"), "DocType").ToUpperInvariant();
        var date = FieldParsers.ParseDate(row.GetField("GLDate"), "GLDate");
        var businessUnit = FieldParsers.Required(row.GetField("BusinessUnit"), "BusinessUnit");
        var amount = FieldParsers.ParseAmount(row.GetField("Amount"), "Amount");
        var currency = FieldParsers.ParseCurrency(row.GetField("Currency"), "Currency");
        var reference = row.GetField("Reference")?.Trim();
        var employeeNo = FieldParsers.Required(row.GetField("EmployeeNo"), "EmployeeNo");
        var description = FieldParsers.Required(row.GetField("Description"), "Description");
        var status = FieldParsers.Required(row.GetField("Status"), "Status").ToUpperInvariant();

        if (status != "POSTED")
            throw new RowParseException($"Belge muhasebeleştirilmemiş (Status={status})");

        var isRefund = docType switch
        {
            "RI" => false,
            "RM" => true,
            _ => throw new RowParseException($"Bilinmeyen belge tipi: {docType}")
        };

        if (amount == 0)
            throw new RowParseException("Tutar sıfır");
        if (!isRefund && amount < 0)
            throw new RowParseException("RI belgesinde negatif tutar");

        var hotel = ProductCatalog.HotelForBusinessUnit(businessUnit)
                    ?? throw new RowParseException($"Bilinmeyen iş birimi: {businessUnit}");

        var productCode = ProductCatalog.CodeForName(description)
                          ?? throw new RowParseException($"Ürün eşleştirilemedi: '{description}'");

        return new ParsedSale
        {
            ExternalDocumentNo = documentNo,
            TransactionDate = date,
            ProductCode = productCode,
            ProductName = description.Trim(),
            Quantity = 1,
            Amount = Math.Abs(amount),
            Currency = currency,
            IsRefund = isRefund,
            RefundReference = isRefund && !string.IsNullOrWhiteSpace(reference) ? reference : null,
            EmployeeNo = employeeNo.ToUpperInvariant(),
            HotelCode = hotel
        };
    }
}
