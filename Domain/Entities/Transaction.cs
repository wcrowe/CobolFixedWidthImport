namespace CobolFixedWidthImport.Domain.Entities;

public sealed class Transaction
{
    public long Id { get; set; }

    public string? RecordType { get; set; }
    public string? AccountNumber { get; set; }
    public DateTime? TransactionDate { get; set; }

    public decimal? Amount { get; set; }

    // Example: variable decimals in file (usually 2, max 5) -> store decimal(19,5)
    public decimal? Field3Example { get; set; }

    // Injection
    public string? SourceSystem { get; set; }
    public string? ImportBatchId { get; set; }
    public DateTime ImportedAtUtc { get; set; }

    public List<TransactionLine> Lines { get; set; } = new();
    public List<TransactionFee> Fees { get; set; } = new();
}
