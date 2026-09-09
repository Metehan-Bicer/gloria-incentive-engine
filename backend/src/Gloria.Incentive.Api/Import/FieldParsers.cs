using System.Globalization;

namespace Gloria.Incentive.Api.Import;

public static class FieldParsers
{
    public static string Required(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new RowParseException($"{fieldName} alanı boş");
        return value.Trim();
    }

    public static DateOnly ParseDate(string? value, string fieldName)
    {
        var text = Required(value, fieldName);
        if (DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date;
        throw new RowParseException($"{fieldName} geçersiz tarih: '{text}' (beklenen biçim yyyy-MM-dd)");
    }

    public static decimal ParseAmount(string? value, string fieldName)
    {
        var text = Required(value, fieldName).Replace(" ", string.Empty);

        var normalized = text;
        if (text.Contains(',') && text.Contains('.'))
            normalized = text.Replace(".", string.Empty).Replace(',', '.');
        else if (text.Contains(','))
            normalized = text.Replace(',', '.');

        if (decimal.TryParse(normalized, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount))
            return amount;

        throw new RowParseException($"{fieldName} geçersiz tutar: '{text}'");
    }

    public static int ParseInt(string? value, string fieldName)
    {
        var text = Required(value, fieldName);
        if (int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number))
            return number;
        throw new RowParseException($"{fieldName} geçersiz sayı: '{text}'");
    }

    public static string ParseCurrency(string? value, string fieldName)
    {
        var text = Required(value, fieldName).ToUpperInvariant();
        if (text != "TRY")
            throw new RowParseException($"Desteklenmeyen para birimi: {text} (yalnızca TRY kabul edilir)");
        return text;
    }
}
