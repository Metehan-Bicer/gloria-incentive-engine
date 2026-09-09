using Gloria.Incentive.Api.Domain;

namespace Gloria.Incentive.Api.Rules.Strategies;

public class TieredRateStrategy : ICommissionRuleStrategy
{
    public RuleType Type => RuleType.TieredRate;
    public string Label => "Kademeli barem";
    public string ParameterHint => """{"mode": "Marginal", "tiers": [{"threshold": 0, "rate": 2}, {"threshold": 30000, "rate": 4}]}""";

    public void ValidateParameters(string parametersJson)
        => RuleParameterJson.Parse<TieredRateParameters>(parametersJson).Validate();

    public RuleOutcome Evaluate(RuleContext context)
    {
        var parameters = RuleParameterJson.Parse<TieredRateParameters>(context.Rule.ParametersJson);
        parameters.Validate();

        var outcome = new RuleOutcome();
        decimal net = 0;

        foreach (var sale in context.Sales.OrderBy(s => s.TransactionDate).ThenBy(s => s.Id))
        {
            net += sale.SignedAmount;
            outcome.Steps.Add(new RuleStep
            {
                LineType = sale.IsRefund ? CalculationLineType.Refund : CalculationLineType.Sale,
                SaleRecordId = sale.Id,
                BaseAmount = sale.SignedAmount,
                Amount = 0,
                Description = sale.IsRefund
                    ? $"İade {sale.SourceSystem}-{sale.ExternalDocumentNo} {sale.ProductName}: {Money.Format(sale.SignedAmount)} net toplamdan düşüldü"
                    : $"{sale.SourceSystem}-{sale.ExternalDocumentNo} {sale.ProductName}: {Money.Format(sale.Amount)} net toplama eklendi"
            });
        }

        if (net <= 0)
        {
            outcome.Steps.Add(new RuleStep
            {
                LineType = CalculationLineType.Tier,
                BaseAmount = net,
                Amount = 0,
                Description = $"Aylık net toplam {Money.Format(net)}, barem uygulanmadı"
            });
            outcome.Subtotal = 0;
            return outcome;
        }

        var tiers = parameters.OrderedTiers;

        if (parameters.Mode == TierMode.Highest)
        {
            var tier = tiers.Last(t => t.Threshold <= net);
            var amount = Money.Round(net * tier.Rate / 100m);
            outcome.Steps.Add(new RuleStep
            {
                LineType = CalculationLineType.Tier,
                BaseAmount = net,
                Rate = tier.Rate,
                Amount = amount,
                Description = $"Aylık net toplam {Money.Format(net)}, ulaşılan dilim eşiği {Money.Format(tier.Threshold)} → tamamına %{tier.Rate}"
            });
            outcome.Subtotal = amount;
            return outcome;
        }

        for (var i = 0; i < tiers.Count; i++)
        {
            var lower = tiers[i].Threshold;
            if (net <= lower) break;

            var upper = i + 1 < tiers.Count ? tiers[i + 1].Threshold : decimal.MaxValue;
            var portion = Math.Min(net, upper) - lower;
            var amount = Money.Round(portion * tiers[i].Rate / 100m);
            var rangeText = upper == decimal.MaxValue
                ? $"{Money.Format(lower)} üzeri"
                : $"{Money.Format(lower)} – {Money.Format(upper)}";

            outcome.Steps.Add(new RuleStep
            {
                LineType = CalculationLineType.Tier,
                BaseAmount = portion,
                Rate = tiers[i].Rate,
                Amount = amount,
                Description = $"Dilim {rangeText}: {Money.Format(portion)} × %{tiers[i].Rate}"
            });
            outcome.Subtotal += amount;
        }

        outcome.Subtotal = Money.Round(outcome.Subtotal);
        return outcome;
    }
}
