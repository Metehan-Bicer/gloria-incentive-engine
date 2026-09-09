namespace Gloria.Incentive.Api.Domain;

public class ImportBatch
{
    public int Id { get; set; }
    public SourceSystem SourceSystem { get; set; }
    public string FileName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int TotalRows { get; set; }
    public int ImportedRows { get; set; }
    public int DuplicateRows { get; set; }
    public int ErrorRows { get; set; }
    public string ImportedBy { get; set; } = string.Empty;

    public ICollection<ImportError> Errors { get; set; } = new List<ImportError>();
}

public class ImportError
{
    public int Id { get; set; }
    public int BatchId { get; set; }
    public ImportBatch Batch { get; set; } = null!;
    public int LineNumber { get; set; }
    public string RawLine { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public bool IsDuplicate { get; set; }
}
