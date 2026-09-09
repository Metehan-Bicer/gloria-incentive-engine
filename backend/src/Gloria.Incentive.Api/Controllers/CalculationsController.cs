using Gloria.Incentive.Api.Auth;
using Gloria.Incentive.Api.Calculation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gloria.Incentive.Api.Controllers;

[ApiController]
[Route("api/calculations")]
[Authorize(Roles = Roles.AdminOrAccounting)]
public class CalculationsController : ControllerBase
{
    private readonly CommissionCalculationService _calculations;

    public CalculationsController(CommissionCalculationService calculations)
    {
        _calculations = calculations;
    }

    [HttpPost("run")]
    public Task<PeriodSummaryDto> Run([FromQuery] int year, [FromQuery] int month, CancellationToken ct)
        => _calculations.RunForAllAsync(year, month, ct);

    [HttpGet("{year:int}/{month:int}")]
    public Task<PeriodSummaryDto> Summary(int year, int month, CancellationToken ct)
        => _calculations.GetPeriodSummaryAsync(year, month, ct);
}
