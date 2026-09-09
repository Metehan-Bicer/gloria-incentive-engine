namespace Gloria.Incentive.Api.Auth;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Accounting = "Muhasebe";
    public const string Employee = "Personel";

    public const string AdminOrAccounting = Admin + "," + Accounting;
    public const string All = Admin + "," + Accounting + "," + Employee;

    public static readonly string[] Known = [Admin, Accounting, Employee];
}
