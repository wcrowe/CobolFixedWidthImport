namespace CobolFixedWidthImport.Domain.Entities;

public sealed class TransactionFee
{
    public long Id { get; set; }

    public long TransactionId { get; set; }
    public Transaction? Transaction { get; set; }

    // Different sequence name than lines (configurable per table)
    public int? FeeSeq { get; set; }

    public string? FeeCode { get; set; }
    public decimal? Amount { get; set; }

    public DateTime ImportedAtUtc { get; set; }
}
