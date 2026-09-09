using Gloria.Incentive.Api.Domain;

namespace Gloria.Incentive.Api.Rules.Strategies;

public class FixedAmountPerTransactionStrategy : ICommissionRuleStrategy
{
    public RuleType Type => RuleType.FixedAmountPerTransaction;
    public string Label => "İşlem başına sabit tutar";
    public string ParameterHint => """{"amount": 50}""";

    public void ValidateParameters(string parametersJson)
        => RuleParameterJson.Parse<FixedAmountParameters>(parametersJson).Validate();

    public RuleOutcome Evaluate(RuleContext context)
    {
        var parameters = RuleParameterJson.Parse<FixedAmountParameters>(context.Rule.ParametersJson);
        parameters.Validate();

        var outcome = new RuleOutcome();

        foreach (var sale in context.Sales.OrderBy(s => s.TransactionDate).ThenBy(s => s.Id))
        {
            var amount = sale.IsRefund ? -parameters.Amount : parameters.Amount;
            outcome.Steps.Add(new RuleStep
            {
                LineType = sale.IsRefund ? CalculationLineType.Refund : CalculationLineType.Sale,
                SaleRecordId = sale.Id,
                BaseAmount = sale.SignedAmount,
                Rate = null,
                Amount = amount,
                Description = sale.IsRefund
                    ? $"İade {sale.SourceSystem}-{sale.ExternalDocumentNo} {sale.ProductName}: işlem başına {Money.Format(parameters.Amount)} düşüldü"
                    : $"{sale.SourceSystem}-{sale.ExternalDocumentNo} {sale.ProductName}: işlem başına {Money.Format(parameters.Amount)}"
            });
            outcome.Subtotal += amount;
        }

        outcome.Subtotal = Money.Round(outcome.Subtotal);
        return outcome;
    }
}
