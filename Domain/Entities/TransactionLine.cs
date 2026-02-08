namespace CobolFixedWidthImport.Domain.Entities;

public sealed class TransactionLine
{
    public long Id { get; set; }

    public long TransactionId { get; set; }
    public Transaction? Transaction { get; set; }

    // Configurable per table name via YAML sequence.target
    public int? LineSeq { get; set; }

    public string? ItemCode { get; set; }
    public decimal? LineAmount { get; set; }
    public long? Quantity { get; set; }
    public string? Notes { get; set; }

    public DateTime ImportedAtUtc { get; set; }
}
