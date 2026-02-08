using CobolFixedWidthImport.Parsing.Config;
using CobolFixedWidthImport.Parsing.FixedWidth;

namespace CobolFixedWidthImport.Parsing.Parsers;

public interface IFieldParser
{
    object? Parse(string raw, FieldDefinition field, LayoutConfig.ParsingRules rules);
}
