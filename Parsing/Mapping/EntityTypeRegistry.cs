using CobolFixedWidthImport.Domain.Entities;

namespace CobolFixedWidthImport.Parsing.Mapping;

/// <summary>
/// Allow-list mapping from YAML entity names to CLR types.
/// Add your 35+ entities here.
/// </summary>
public sealed class EntityTypeRegistry
{
    private readonly Dictionary<string, Type> _map = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Transaction"] = typeof(Transaction),
        ["TransactionLine"] = typeof(TransactionLine),
        ["TransactionFee"] = typeof(TransactionFee),
        ["Account"] = typeof(Account),
    };

    public Type Resolve(string name)
        => _map.TryGetValue(name, out var t)
            ? t
            : throw new InvalidOperationException($"Unknown entity '{name}'. Register it in EntityTypeRegistry.");
}
