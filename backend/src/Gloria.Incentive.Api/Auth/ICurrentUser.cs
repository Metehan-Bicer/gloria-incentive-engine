namespace Gloria.Incentive.Api.Auth;

public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    string Role { get; }
    string? EmployeeNo { get; }
    string DisplayName { get; }
    bool IsInRole(string role);
}

public class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _accessor;

    public HttpCurrentUser(IHttpContextAccessor accessor)
    {
        _accessor = accessor;
    }

    private System.Security.Claims.ClaimsPrincipal? Principal => _accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public string Role => Principal?.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;

    public string? EmployeeNo => Principal?.FindFirst(HeaderAuthenticationHandler.EmployeeNoClaim)?.Value;

    public string DisplayName => IsAuthenticated
        ? (EmployeeNo is null ? Role : $"{Role}:{EmployeeNo}")
        : "system";

    public bool IsInRole(string role) => Principal?.IsInRole(role) == true;
}
