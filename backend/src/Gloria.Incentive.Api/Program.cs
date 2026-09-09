using Gloria.Incentive.Api.Auth;
using Gloria.Incentive.Api.Calculation;
using Gloria.Incentive.Api.Import;
using Gloria.Incentive.Api.Import.Parsers;
using Gloria.Incentive.Api.Infrastructure;
using Gloria.Incentive.Api.Periods;
using Gloria.Incentive.Api.Data;
using Gloria.Incentive.Api.Rules;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default")));
builder.Services.AddScoped<DataSeeder>();
builder.Services.AddCommissionRules();
builder.Services.AddScoped<CommissionCalculator>();
builder.Services.AddScoped<CommissionCalculationService>();
builder.Services.AddScoped<PeriodService>();
builder.Services.AddSingleton<ISourceParser, PmsParser>();
builder.Services.AddSingleton<ISourceParser, PosParser>();
builder.Services.AddSingleton<ISourceParser, ErpParser>();
builder.Services.AddScoped<CsvImportService>();
builder.Services.AddScoped<SampleDataImporter>();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services
    .AddAuthentication(HeaderAuthenticationHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>(HeaderAuthenticationHandler.SchemeName, null);
builder.Services.AddAuthorization();

builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
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

app.UseExceptionHandler();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await scope.ServiceProvider.GetRequiredService<DataSeeder>().SeedAsync();
    await scope.ServiceProvider.GetRequiredService<SampleDataImporter>().ImportIfEmptyAsync();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
