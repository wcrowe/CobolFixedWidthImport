namespace CobolFixedWidthImport.Domain.Entities;

public sealed class Account
{
    public long Id { get; set; }

    public string? AccountNumber { get; set; }
    public string? AccountName { get; set; }
    public decimal? Balance { get; set; }

    public string? SourceSystem { get; set; }
    public string? ImportBatchId { get; set; }
    public DateTime ImportedAtUtc { get; set; }
}
