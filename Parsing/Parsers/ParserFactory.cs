using CobolFixedWidthImport.Parsing.Config;
using CobolFixedWidthImport.Parsing.FixedWidth;

namespace CobolFixedWidthImport.Parsing.Parsers;

public sealed class ParserFactory
{
    private readonly IFieldParser _date = new DateFieldParser();
    private readonly IFieldParser _numeric = new NumericFieldParser();
    private readonly IFieldParser _integer = new IntegerFieldParser();
    private readonly IFieldParser _string = new StringFieldParser();
    private readonly IFieldParser _bool = new BooleanFieldParser();

    public IFieldParser Create(FieldDefinition field, LayoutConfig.ParsingRules rules)
        => field.Type.Trim().ToLowerInvariant() switch
        {
            "date" => _date,
            "numeric" => _numeric,
            "integer" => _integer,
            "string" => _string,
            "boolean" or "bool" => _bool,
            _ => _string
        };
}
