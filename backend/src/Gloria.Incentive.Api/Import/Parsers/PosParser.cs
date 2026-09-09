using CsvHelper;
using Gloria.Incentive.Api.Domain;

namespace Gloria.Incentive.Api.Import.Parsers;

public class PosParser : ISourceParser
{
    public SourceSystem Source => SourceSystem.POS;

    public IReadOnlyList<string> RequiredHeaders { get; } =
        ["FisNo", "Tarih", "PLU", "UrunAdi", "Adet", "BirimFiyat", "ToplamTutar", "SatisPersoneli", "IadeMi"];

    public ParsedSale Parse(IReaderRow row)
    {
        var receiptNo = FieldParsers.Required(row.GetField("FisNo"), "FisNo");
        var date = FieldParsers.ParseDate(row.GetField("Tarih"), "Tarih");
        var plu = FieldParsers.Required(row.GetField("PLU"), "PLU");
        var productName = FieldParsers.Required(row.GetField("UrunAdi"), "UrunAdi");
        var quantity = FieldParsers.ParseInt(row.GetField("Adet"), "Adet");
        var total = FieldParsers.ParseAmount(row.GetField("ToplamTutar"), "ToplamTutar");
        var employeeNo = FieldParsers.Required(row.GetField("SatisPersoneli"), "SatisPersoneli");
        var refundFlag = FieldParsers.Required(row.GetField("IadeMi"), "IadeMi").ToUpperInvariant();

        var isRefund = refundFlag switch
        {
            "H" => false,
            "E" => true,
            _ => throw new RowParseException($"IadeMi alanı E veya H olmalı: '{refundFlag}'")
        };

        if (quantity == 0 || total == 0)
            throw new RowParseException("Adet veya tutar sıfır");
        if (!isRefund && total < 0)
            throw new RowParseException("İade olmayan satırda negatif tutar");

        return new ParsedSale
        {
            ExternalDocumentNo = receiptNo,
            TransactionDate = date,
            ProductCode = plu,
            ProductName = productName,
            Quantity = Math.Abs(quantity),
            Amount = Math.Abs(total),
            Currency = "TRY",
            IsRefund = isRefund,
            EmployeeNo = employeeNo.ToUpperInvariant()
        };
    }
}
