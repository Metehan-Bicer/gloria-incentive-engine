using Gloria.Incentive.Api.Auth;
using Gloria.Incentive.Api.Calculation;
using Gloria.Incentive.Api.Data;
using Gloria.Incentive.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Gloria.Incentive.Api.Controllers;

[ApiController]
[Route("api/employees")]
[Authorize(Roles = Roles.All)]
public class EmployeesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly CommissionCalculationService _calculations;
    private readonly ICurrentUser _currentUser;

    public EmployeesController(AppDbContext db, CommissionCalculationService calculations, ICurrentUser currentUser)
    {
        _db = db;
        _calculations = calculations;
        _currentUser = currentUser;
    }

    public record EmployeeDto(int Id, string EmployeeNo, string FullName, string Department, string HotelCode, DateOnly HireDate, DateOnly? TerminationDate);

    [HttpGet]
    public async Task<IReadOnlyList<EmployeeDto>> List(CancellationToken ct)
    {
        var query = _db.Employees.Include(e => e.Department).AsQueryable();

        if (_currentUser.IsInRole(Roles.Employee))
            query = query.Where(e => e.EmployeeNo == _currentUser.EmployeeNo);

        return await query
            .OrderBy(e => e.EmployeeNo)
            .Select(e => new EmployeeDto(e.Id, e.EmployeeNo, e.FullName, e.Department.Name, e.HotelCode, e.HireDate, e.TerminationDate))
            .ToListAsync(ct);
    }

    [HttpGet("{employeeNo}/commissions/{year:int}/{month:int}")]
    public async Task<CommissionResultDto> GetCommission(string employeeNo, int year, int month, CancellationToken ct)
    {
        employeeNo = employeeNo.Trim().ToUpperInvariant();

        if (_currentUser.IsInRole(Roles.Employee) && !string.Equals(_currentUser.EmployeeNo, employeeNo, StringComparison.Ordinal))
            throw new ForbiddenException("Personel yalnızca kendi prim hesabını görüntüleyebilir.");

        return await _calculations.GetOrCalculateAsync(employeeNo, year, month, ct);
    }
}
