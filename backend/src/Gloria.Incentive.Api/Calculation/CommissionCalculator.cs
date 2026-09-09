using Gloria.Incentive.Api.Domain;
using Gloria.Incentive.Api.Rules;

namespace Gloria.Incentive.Api.Calculation;

public class CommissionCalculator
{
    private readonly RuleStrategyResolver _resolver;

    public CommissionCalculator(RuleStrategyResolver resolver)
    {
        _resolver = resolver;
    }

    public CalculationResult Calculate(Employee employee, IReadOnlyCollection<SaleRecord> sales, IReadOnlyCollection<CommissionRule> rules, int year, int month)
    {
        var result = new CalculationResult();
        var sequence = 0;

        var eligible = new List<SaleRecord>();
        foreach (var sale in sales.OrderBy(s => s.TransactionDate).ThenBy(s => s.Id))
        {
            if (sale.Amount <= 0)
            {
                AddLine(result, ref sequence, CalculationLineType.Excluded, sale, null, sale.SignedAmount, null, 0,
                    $"{sale.SourceSystem}-{sale.ExternalDocumentNo}: tutar sıfır, hesaba dahil edilmedi");
                continue;
            }

            if (!employee.IsEmployedOn(sale.TransactionDate))
            {
                var reason = sale.TransactionDate < employee.HireDate
                    ? $"işe başlama tarihi ({employee.HireDate:yyyy-MM-dd}) öncesi"
                    : $"işten ayrılış tarihi ({employee.TerminationDate:yyyy-MM-dd}) sonrası";
                AddLine(result, ref sequence, CalculationLineType.Excluded, sale, null, sale.SignedAmount, null, 0,
                    $"{sale.SourceSystem}-{sale.ExternalDocumentNo} {sale.ProductName}: {reason}, hesaba dahil edilmedi");
                continue;
            }

            eligible.Add(sale);
        }

        result.GrossSales = eligible.Where(s => !s.IsRefund).Sum(s => s.Amount);
        result.RefundTotal = eligible.Where(s => s.IsRefund).Sum(s => s.Amount);

        var matchedSaleIds = new HashSet<int>();
        decimal total = 0;

        foreach (var rule in rules.Where(r => r.IsActive).OrderBy(r => r.Priority).ThenBy(r => r.Id))
        {
            var matching = eligible
                .Where(s => rule.IsValidOn(s.TransactionDate) && rule.AppliesTo(s))
                .ToList();

            if (matching.Count == 0) continue;

            var outcome = _resolver.Resolve(rule.RuleType).Evaluate(new RuleContext
            {
                Rule = rule,
                Employee = employee,
                Sales = matching,
                Year = year,
                Month = month
            });

            foreach (var step in outcome.Steps)
            {
                result.Lines.Add(new CalculationLine
                {
                    Sequence = ++sequence,
                    LineType = step.LineType,
                    SaleRecordId = step.SaleRecordId,
                    RuleId = rule.Id,
                    RuleName = rule.Name,
                    BaseAmount = step.BaseAmount,
                    Rate = step.Rate,
                    Amount = step.Amount,
                    Description = step.Description
                });
                if (step.SaleRecordId is int id) matchedSaleIds.Add(id);
            }

            result.Lines.Add(new CalculationLine
            {
                Sequence = ++sequence,
                LineType = CalculationLineType.RuleSubtotal,
                RuleId = rule.Id,
                RuleName = rule.Name,
                BaseAmount = matching.Sum(s => s.SignedAmount),
                Amount = outcome.Subtotal,
                Description = $"{rule.Name} ara toplamı ({matching.Count} işlem)"
            });

            total += outcome.Subtotal;
        }

        foreach (var sale in eligible.Where(s => !matchedSaleIds.Contains(s.Id)))
        {
            AddLine(result, ref sequence, CalculationLineType.Excluded, sale, null, sale.SignedAmount, null, 0,
                $"{sale.SourceSystem}-{sale.ExternalDocumentNo} {sale.ProductName} ({sale.ProductCode}): uygun prim kuralı yok");
        }

        result.TotalCommission = Math.Max(0, Math.Round(total, 2, MidpointRounding.AwayFromZero));
        result.Lines.Add(new CalculationLine
        {
            Sequence = ++sequence,
            LineType = CalculationLineType.Total,
            BaseAmount = result.GrossSales - result.RefundTotal,
            Amount = result.TotalCommission,
            Description = total < 0
                ? $"Kural toplamı negatif ({Money.Format(total)}), prim sıfırlandı"
                : $"{year}-{month:00} dönemi toplam prim"
        });

        return result;
    }

    private static void AddLine(CalculationResult result, ref int sequence, CalculationLineType type, SaleRecord? sale, CommissionRule? rule,
        decimal baseAmount, decimal? rate, decimal amount, string description)
    {
        result.Lines.Add(new CalculationLine
        {
            Sequence = ++sequence,
            LineType = type,
            SaleRecordId = sale?.Id,
            RuleId = rule?.Id,
            RuleName = rule?.Name,
            BaseAmount = baseAmount,
            Rate = rate,
            Amount = amount,
            Description = description
        });
    }
}
