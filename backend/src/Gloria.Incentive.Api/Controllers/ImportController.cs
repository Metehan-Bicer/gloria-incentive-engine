using Gloria.Incentive.Api.Auth;
using Gloria.Incentive.Api.Domain;
using Gloria.Incentive.Api.Import;
using Gloria.Incentive.Api.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Gloria.Incentive.Api.Controllers;

[ApiController]
[Route("api/import")]
[Authorize(Roles = Roles.AdminOrAccounting)]
public class ImportController : ControllerBase
{
    private readonly CsvImportService _import;

    public ImportController(CsvImportService import)
    {
        _import = import;
    }

    [HttpPost("{source}")]
    [RequestSizeLimit(20_000_000)]
    public async Task<ImportResultDto> Upload(string source, IFormFile file, CancellationToken ct)
    {
        if (!Enum.TryParse<SourceSystem>(source, true, out var sourceSystem))
            throw new BadRequestException("Kaynak sistem PMS, POS veya ERP olmalıdır.");

        if (file is null || file.Length == 0)
            throw new BadRequestException("Yüklenecek dosya boş.");

        await using var stream = file.OpenReadStream();
        return await _import.ImportAsync(sourceSystem, file.FileName, stream, ct);
    }

    [HttpGet("batches")]
    public Task<IReadOnlyList<ImportBatchDto>> Batches(CancellationToken ct) => _import.ListBatchesAsync(ct);

    [HttpGet("batches/{id:int}/errors")]
    public Task<IReadOnlyList<ImportErrorDto>> Errors(int id, CancellationToken ct) => _import.ListErrorsAsync(id, ct);
}
