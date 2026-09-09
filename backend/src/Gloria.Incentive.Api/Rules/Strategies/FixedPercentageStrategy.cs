using Gloria.Incentive.Api.Domain;

namespace Gloria.Incentive.Api.Rules.Strategies;

public class FixedPercentageStrategy : ICommissionRuleStrategy
{
    public RuleType Type => RuleType.FixedPercentage;
    public string Label => "Sabit yüzde";
    public string ParameterHint => """{"percentage": 5}""";

    public void ValidateParameters(string parametersJson)
        => RuleParameterJson.Parse<FixedPercentageParameters>(parametersJson).Validate();

    public RuleOutcome Evaluate(RuleContext context)
    {
        var parameters = RuleParameterJson.Parse<FixedPercentageParameters>(context.Rule.ParametersJson);
        parameters.Validate();

        var outcome = new RuleOutcome();
        var rate = parameters.Percentage;

        foreach (var sale in context.Sales.OrderBy(s => s.TransactionDate).ThenBy(s => s.Id))
        {
            var amount = Money.Round(sale.SignedAmount * rate / 100m);
            outcome.Steps.Add(new RuleStep
            {
                LineType = sale.IsRefund ? CalculationLineType.Refund : CalculationLineType.Sale,
                SaleRecordId = sale.Id,
                BaseAmount = sale.SignedAmount,
                Rate = rate,
                Amount = amount,
                Description = sale.IsRefund
                    ? $"İade {sale.SourceSystem}-{sale.ExternalDocumentNo} {sale.ProductName}: {Money.Format(sale.SignedAmount)} × %{rate}"
                    : $"{sale.SourceSystem}-{sale.ExternalDocumentNo} {sale.ProductName}: {Money.Format(sale.Amount)} × %{rate}"
            });
            outcome.Subtotal += amount;
        }

        outcome.Subtotal = Money.Round(outcome.Subtotal);
        return outcome;
    }
}
