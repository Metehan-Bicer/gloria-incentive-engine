using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Gloria.Incentive.Api.Auth;

public class HeaderAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Header";
    public const string RoleHeader = "X-Role";
    public const string EmployeeNoHeader = "X-Employee-No";
    public const string EmployeeNoClaim = "employee_no";

    public HeaderAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(RoleHeader, out var roleValues))
            return Task.FromResult(AuthenticateResult.NoResult());

        var role = Roles.Known.FirstOrDefault(r => string.Equals(r, roleValues.ToString().Trim(), StringComparison.OrdinalIgnoreCase));
        if (role is null)
            return Task.FromResult(AuthenticateResult.Fail($"Unknown role '{roleValues}'."));

        var employeeNo = Request.Headers.TryGetValue(EmployeeNoHeader, out var noValues)
            ? noValues.ToString().Trim().ToUpperInvariant()
            : null;

        if (role == Roles.Employee && string.IsNullOrWhiteSpace(employeeNo))
            return Task.FromResult(AuthenticateResult.Fail($"{EmployeeNoHeader} header is required for role {Roles.Employee}."));

        var claims = new List<Claim>
        {
            new(ClaimTypes.Role, role),
            new(ClaimTypes.Name, string.IsNullOrWhiteSpace(employeeNo) ? role : employeeNo)
        };
        if (!string.IsNullOrWhiteSpace(employeeNo))
            claims.Add(new Claim(EmployeeNoClaim, employeeNo));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
