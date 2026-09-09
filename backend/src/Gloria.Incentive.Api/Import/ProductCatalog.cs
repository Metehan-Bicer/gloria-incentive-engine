namespace Gloria.Incentive.Api.Import;

public static class ProductCatalog
{
    private static readonly Dictionary<string, string> PrefixCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SPA_"] = "SPA",
        ["ALC_"] = "ALC",
        ["BUG_"] = "BUGGY",
        ["PAV_"] = "PAVILLON",
        ["GLF_"] = "GOLF"
    };

    private static readonly Dictionary<string, string> NameToCode = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SPA Masaj 60dk"] = "SPA_MSJ60",
        ["SPA Masaj 90dk"] = "SPA_MSJ90",
        ["Hamam Paketi"] = "SPA_HMM",
        ["A la Carte Italian"] = "ALC_ITL",
        ["A la Carte Fish"] = "ALC_FSH",
        ["A la Carte Turkish"] = "ALC_TRK",
        ["Buggy 4 Saat"] = "BUG_4H",
        ["Buggy Günlük"] = "BUG_DAY",
        ["Pavillon Standart"] = "PAV_STD",
        ["Pavillon VIP"] = "PAV_VIP",
        ["Golf Dersi"] = "GLF_LSN"
    };

    private static readonly Dictionary<string, string> BusinessUnitHotels = new()
    {
        ["1100"] = "GSR",
        ["1200"] = "GGR",
        ["1300"] = "GVR"
    };

    public static string CategoryFor(string productCode)
    {
        foreach (var (prefix, category) in PrefixCategories)
        {
            if (productCode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return category;
        }

        if (productCode.Length == 4 && productCode.All(char.IsDigit))
        {
            return productCode[0] switch
            {
                '5' => "SPA_RETAIL",
                '6' => "ALC_EXTRA",
                '7' => "BAR",
                _ => "OTHER"
            };
        }

        return "OTHER";
    }

    public static string? CodeForName(string productName)
        => NameToCode.TryGetValue(productName.Trim(), out var code) ? code : null;

    public static string? HotelForBusinessUnit(string businessUnit)
        => BusinessUnitHotels.TryGetValue(businessUnit.Trim(), out var hotel) ? hotel : null;

    public static IReadOnlyCollection<string> KnownCategories { get; } =
        ["SPA", "ALC", "BUGGY", "PAVILLON", "GOLF", "SPA_RETAIL", "ALC_EXTRA", "BAR", "OTHER"];
}
