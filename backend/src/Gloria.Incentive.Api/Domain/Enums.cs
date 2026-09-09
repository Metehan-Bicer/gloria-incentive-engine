namespace Gloria.Incentive.Api.Domain;

public enum SourceSystem
{
    PMS = 1,
    POS = 2,
    ERP = 3
}

public enum RuleType
{
    FixedPercentage = 1,
    TieredRate = 2,
    FixedAmountPerTransaction = 3
}

public enum PeriodStatus
{
    Open = 1,
    Closed = 2
}

public enum CalculationLineType
{
    Sale = 1,
    Refund = 2,
    Excluded = 3,
    RuleSubtotal = 4,
    Total = 5,
    Tier = 6
}

public enum AuditAction
{
    Create = 1,
    Update = 2,
    Delete = 3
}
