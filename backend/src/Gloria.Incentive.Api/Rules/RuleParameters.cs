using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gloria.Incentive.Api.Rules;

public static class RuleParameterJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    public static T Parse<T>(string json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new RuleParameterException("Kural parametreleri boş olamaz.");

        try
        {
            return JsonSerializer.Deserialize<T>(json, Options)
                   ?? throw new RuleParameterException("Kural parametreleri çözümlenemedi.");
        }
        catch (JsonException ex)
        {
            throw new RuleParameterException($"Kural parametreleri geçersiz JSON: {ex.Message}");
        }
    }
}

public class RuleParameterException : Exception
{
    public RuleParameterException(string message) : base(message)
    {
    }
}

public class FixedPercentageParameters
{
    public decimal Percentage { get; set; }

    public void Validate()
    {
        if (Percentage <= 0 || Percentage > 100)
            throw new RuleParameterException("Yüzde 0 ile 100 arasında olmalıdır.");
    }
}

public class FixedAmountParameters
{
    public decimal Amount { get; set; }

    public void Validate()
    {
        if (Amount <= 0)
            throw new RuleParameterException("İşlem başına tutar sıfırdan büyük olmalıdır.");
    }
}

public enum TierMode
{
    Marginal,
    Highest
}

public class TieredRateParameters
{
    public TierMode Mode { get; set; } = TierMode.Marginal;
    public List<Tier> Tiers { get; set; } = new();

    public void Validate()
    {
        if (Tiers.Count == 0)
            throw new RuleParameterException("En az bir barem dilimi tanımlanmalıdır.");

        var ordered = Tiers.OrderBy(t => t.Threshold).ToList();
        if (ordered[0].Threshold != 0)
            throw new RuleParameterException("İlk barem dilimi 0 eşiğinden başlamalıdır.");

        for (var i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].Threshold == ordered[i - 1].Threshold)
                throw new RuleParameterException("Barem eşikleri birbirinden farklı olmalıdır.");
        }

        if (ordered.Any(t => t.Rate < 0 || t.Rate > 100))
            throw new RuleParameterException("Barem oranları 0 ile 100 arasında olmalıdır.");
    }

    public IReadOnlyList<Tier> OrderedTiers => Tiers.OrderBy(t => t.Threshold).ToList();
}

public class Tier
{
    public decimal Threshold { get; set; }
    public decimal Rate { get; set; }
}
