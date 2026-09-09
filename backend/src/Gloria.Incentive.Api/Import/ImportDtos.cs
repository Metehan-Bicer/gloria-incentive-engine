using Gloria.Incentive.Api.Domain;

namespace Gloria.Incentive.Api.Import;

public record ImportBatchDto(
    int Id,
    SourceSystem SourceSystem,
    string FileName,
    DateTime StartedAt,
    DateTime? FinishedAt,
    int TotalRows,
    int ImportedRows,
    int DuplicateRows,
    int ErrorRows,
    string ImportedBy);

public record ImportErrorDto(int Id, int LineNumber, string RawLine, string Reason, bool IsDuplicate);

public record ImportResultDto(ImportBatchDto Batch, IReadOnlyList<ImportErrorDto> Errors);
