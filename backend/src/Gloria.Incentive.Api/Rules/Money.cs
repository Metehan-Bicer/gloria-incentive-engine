namespace Gloria.Incentive.Api.Rules;

public static class Money
{
    private static readonly System.Globalization.CultureInfo Culture = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");

    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static string Format(decimal value) => value.ToString("N2", Culture) + " TL";
}
