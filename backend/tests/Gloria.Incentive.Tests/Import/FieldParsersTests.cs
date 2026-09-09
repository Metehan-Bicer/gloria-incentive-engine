using Gloria.Incentive.Api.Import;

namespace Gloria.Incentive.Tests.Import;

public class FieldParsersTests
{
    [Theory]
    [InlineData("2500.00", 2500)]
    [InlineData("2.500,00", 2500)]
    [InlineData("2500,50", 2500.5)]
    [InlineData("-3900.00", -3900)]
    [InlineData("13500", 13500)]
    public void Parses_invariant_and_turkish_number_formats(string input, decimal expected)
    {
        Assert.Equal(expected, FieldParsers.ParseAmount(input, "Tutar"));
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData("  ")]
    public void Rejects_invalid_amounts(string input)
    {
        Assert.Throws<RowParseException>(() => FieldParsers.ParseAmount(input, "Tutar"));
    }

    [Theory]
    [InlineData("32/08/2026")]
    [InlineData("08.15.2026")]
    [InlineData("2026-13-01")]
    [InlineData("")]
    public void Rejects_invalid_dates(string input)
    {
        Assert.Throws<RowParseException>(() => FieldParsers.ParseDate(input, "Tarih"));
    }

    [Fact]
    public void Accepts_iso_date()
    {
        Assert.Equal(new DateOnly(2026, 8, 15), FieldParsers.ParseDate("2026-08-15", "Tarih"));
    }

    [Theory]
    [InlineData("EUR")]
    [InlineData("USD")]
    public void Rejects_foreign_currencies(string currency)
    {
        Assert.Throws<RowParseException>(() => FieldParsers.ParseCurrency(currency, "ParaBirimi"));
    }
}
