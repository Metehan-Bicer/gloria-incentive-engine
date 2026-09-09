using CsvHelper;
using Gloria.Incentive.Api.Domain;

namespace Gloria.Incentive.Api.Import.Parsers;

public class PmsParser : ISourceParser
{
    public SourceSystem Source => SourceSystem.PMS;

    public IReadOnlyList<string> RequiredHeaders { get; } =
        ["BelgeNo", "IslemTarihi", "UrunKodu", "UrunAdi", "Adet", "Tutar", "ParaBirimi", "KasiyerNo", "IslemTipi", "Otel"];

    public ParsedSale Parse(IReaderRow row)
    {
        var documentNo = FieldParsers.Required(row.GetField("BelgeNo"), "BelgeNo");
        var date = FieldParsers.ParseDate(row.GetField("IslemTarihi"), "IslemTarihi");
        var productCode = FieldParsers.Required(row.GetField("UrunKodu"), "UrunKodu");
        var productName = FieldParsers.Required(row.GetField("UrunAdi"), "UrunAdi");
        var quantity = FieldParsers.ParseInt(row.GetField("Adet"), "Adet");
        var amount = FieldParsers.ParseAmount(row.GetField("Tutar"), "Tutar");
        var currency = FieldParsers.ParseCurrency(row.GetField("ParaBirimi"), "ParaBirimi");
        var employeeNo = FieldParsers.Required(row.GetField("KasiyerNo"), "KasiyerNo");
        var transactionType = FieldParsers.Required(row.GetField("IslemTipi"), "IslemTipi").ToUpperInvariant();
        var hotel = FieldParsers.Required(row.GetField("Otel"), "Otel");

        var isRefund = transactionType switch
        {
            "POSTING" => false,
            "REVERSAL" => true,
            _ => throw new RowParseException($"Bilinmeyen işlem tipi: {transactionType}")
        };

        if (amount == 0)
            throw new RowParseException("Tutar sıfır");
        if (!isRefund && amount < 0)
            throw new RowParseException("POSTING satırında negatif tutar");

        return new ParsedSale
        {
            ExternalDocumentNo = documentNo,
            TransactionDate = date,
            ProductCode = productCode.ToUpperInvariant(),
            ProductName = productName,
            Quantity = Math.Abs(quantity),
            Amount = Math.Abs(amount),
            Currency = currency,
            IsRefund = isRefund,
            EmployeeNo = employeeNo.ToUpperInvariant(),
            HotelCode = hotel.ToUpperInvariant()
        };
    }
}
