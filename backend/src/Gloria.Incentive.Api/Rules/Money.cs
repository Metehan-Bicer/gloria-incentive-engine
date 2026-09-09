namespace Gloria.Incentive.Api.Rules;

internal static class Money
{
    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static string Format(decimal value) => value.ToString("N2", new System.Globalization.CultureInfo("tr-TR")) + " TL";
}
