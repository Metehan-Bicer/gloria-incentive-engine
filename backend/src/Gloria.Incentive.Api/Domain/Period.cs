namespace Gloria.Incentive.Api.Domain;

public class Period
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public PeriodStatus Status { get; set; } = PeriodStatus.Open;
    public DateTime? ClosedAt { get; set; }
    public string? ClosedBy { get; set; }

    public bool IsClosed => Status == PeriodStatus.Closed;
}
