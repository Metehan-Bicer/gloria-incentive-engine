using Gloria.Incentive.Api.Auth;
using Gloria.Incentive.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<DataSeeder>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services
    .AddAuthentication(HeaderAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>(HeaderAuthenticationHandler.SchemeName, null);
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Role", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = HeaderAuthenticationHandler.RoleHeader,
        Description = "Admin, Muhasebe veya Personel"
    });
    options.AddSecurityDefinition("EmployeeNo", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = HeaderAuthenticationHandler.EmployeeNoHeader,
        Description = "Personel rolü için sicil numarası (örn. P1001)"
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Role", document)] = [],
        [new OpenApiSecuritySchemeReference("EmployeeNo", document)] = []
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<DataSeeder>().SeedAsync();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
