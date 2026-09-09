namespace Gloria.Incentive.Api.Domain;

public class Employee
{
    public int Id { get; set; }
    public string EmployeeNo { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public string HotelCode { get; set; } = string.Empty;
    public DateOnly HireDate { get; set; }
    public DateOnly? TerminationDate { get; set; }

    public bool IsEmployedOn(DateOnly date)
    {
        if (date < HireDate) return false;
        return TerminationDate is null || date <= TerminationDate.Value;
    }
}
