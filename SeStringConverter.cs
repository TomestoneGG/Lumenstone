using System;
using System.Linq.Expressions;
using System.Reflection.Metadata.Ecma335;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets2;
using Lumina.Text;
using Lumina.Text.Payloads;
using SixLabors.ImageSharp.Formats;

public class SeStringConverter : JsonConverter<SeString>
{
    private ExcelSheet<UIColor> _uiColors;

    public SeStringConverter(ExcelSheet<UIColor> uiColors)
    {
        
        _uiColors = uiColors;
    }

    public override SeString Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // For deserialization, you might want to convert the JSON string back to SeString.
        // This example assumes you're not deserializing SeString objects.
        throw new NotImplementedException("Deserialization is not supported.");
    }

    public override void Write(Utf8JsonWriter writer, SeString value, JsonSerializerOptions options)
    {
        // Serialize the SeString object as a JSON string.
        try { writer.WriteStringValue(ConvertSeString(value)); } catch (Exception) {
            writer.WriteNullValue();
        }
    }

    public String ConvertSeString(SeString value)
    {
        return string.Join("", value.Payloads.Select(ConvertPayload));
    }

    public string ConvertPayloadTag(String type)
    {
        // Convert italics to HTML <i> and </i> tags.
        var lowerType = type.ToLower();
        if (lowerType == "<italics>1</italics>")
            return "<i>";
        if (lowerType == "<italics>0</italics>")
            return "</i>";

        // Eliminate uicolorborder ("UIGlow") as we don't bother representing it in any way.
        if (lowerType.StartsWith("<uicolorborder")) {
            return "";
        }

        // Convert ui color fill to a span with a foreground color.
        if (lowerType.StartsWith("<uicolorfill")) {
            if (lowerType == "<uicolorfill>0</uicolorfill>")
                return "</span>";
            string numPattern = @"<uicolorfill>(\d+)</uicolorfill>";
        
            Match match = Regex.Match(lowerType, numPattern);
            string colorAttr = "";
            if (match.Success) {
                string numStr = match.Groups[1].Value; // Extract the number as a string
                uint num = uint.Parse(numStr); // Convert the string to an integer
                var row = _uiColors.GetRow(num);
                if (row != null) {
                    var colorProperty = typeof(UIColor).GetProperty("UIForeground");
                    if (colorProperty != null && colorProperty.PropertyType == typeof(uint)) {
                        uint colorValue = (uint)colorProperty.GetValue(row);
                        string hexValue = colorValue.ToString("x");
                        string paddedHexValue = hexValue.PadLeft(8, '0');
                        string firstSixChars = paddedHexValue.Substring(0, 6);
                        colorAttr = " style=\"color: #" + firstSixChars + ";\"";
                    }
                }
            }

            return $"<span{colorAttr}>";
        }
    
        return type;
    }

    public String ConvertExpression(Lumina.Text.Expressions.BaseExpression expression)
    {
        if (expression is Lumina.Text.Expressions.StringExpression se)
            return ConvertSeString(se.Value);
        if (expression is Lumina.Text.Expressions.BinaryExpression be)
            return ConvertBinaryExpression(be);
         if (expression is Lumina.Text.Expressions.ParameterExpression pe)
            return ConvertParameterExpression(pe);
        return expression.ToString();
    }

    public String ConvertParameterExpression(Lumina.Text.Expressions.ParameterExpression expression)
    {
        return expression.ExpressionType switch
        {
            Lumina.Text.Expressions.ExpressionType.IntegerParameter => $"IntegerParameter({ConvertExpression(expression.Operand)})",
            Lumina.Text.Expressions.ExpressionType.PlayerParameter => $"PlayerParameter({ConvertExpression(expression.Operand)})",
            Lumina.Text.Expressions.ExpressionType.StringParameter => $"StringParameter({ConvertExpression(expression.Operand)})",
            Lumina.Text.Expressions.ExpressionType.ObjectParameter => $"ObjectParameter({ConvertExpression(expression.Operand)})",
            _ => throw new NotImplementedException() // cannot reach, as this instance is immutable and this field is filtered from constructor
        };
    }

    public String ConvertBinaryExpression(Lumina.Text.Expressions.BinaryExpression expression)
    {
        var first = ConvertExpression(expression.Operand1);
        var second = ConvertExpression(expression.Operand2);
        
        var result = expression.ExpressionType switch
        {
            Lumina.Text.Expressions.ExpressionType.GreaterThanOrEqualTo => $"GreaterThanOrEqualTo({first},{second})",
            Lumina.Text.Expressions.ExpressionType.GreaterThan => $"GreaterThan({first},{second})",
            Lumina.Text.Expressions.ExpressionType.LessThanOrEqualTo => $"LessThanOrEqualTo({first},{second})",
            Lumina.Text.Expressions.ExpressionType.LessThan => $"LessThan({first},{second})",
            Lumina.Text.Expressions.ExpressionType.Equal => $"Equal({first},{second})",
            Lumina.Text.Expressions.ExpressionType.NotEqual => $"NotEqual({first},{second})",
            _ => throw new NotImplementedException() // cannot reach, as this instance is immutable and this field is filtered from constructor
        };

        // Define the regular expression pattern
         // \b matches a word boundary, gnum matches the literal string "gnum",
        // \d+ matches one or more digits, and (?=\D) is a positive lookahead that asserts the next character is not a digit
        string pattern = @"\bgnum(\d+)(?=\D)";

        // Define the replacement string using a MatchEvaluator delegate
        // This allows us to dynamically construct the replacement string based on the matched digits
        return Regex.Replace(result, pattern, match => $"PlayerParameter({match.Groups[1].Value})");
    }

    public String ConvertPayload(BasePayload payload)
    {
        if( payload.PayloadType == PayloadType.Text )
            return payload.RawString.Replace( "<", "\\<" );

         switch( payload.PayloadType )
            {
                case PayloadType.NewLine:
                    return "\n";
                case PayloadType.SoftHyphen:
                    return "";
                case PayloadType.Hyphen:
                    return "–";
                default:
                {
                    if( payload.Expressions.Count == 0 )
                        return ConvertPayloadTag($"<{payload.PayloadType.ToString().ToLower()}>");

                    var originalPayloadTag = payload.PayloadType.ToString();
                    var payloadTag = originalPayloadTag.ToLower();
                    if (payloadTag == "if" && payload.Expressions.Count == 3) {
                        string expression1 = ConvertExpression(payload.Expressions.ElementAt(0));
                        string expression2 = ConvertExpression(payload.Expressions.ElementAt(1));
                        string expression3 = ConvertExpression(payload.Expressions.ElementAt(2));
                        return $"<If({expression1})>{expression2}<Else/>{expression3}</If>";
                    }
                    
                    if (payload.Expressions.Count == 1) {
                        string expression1 = ConvertExpression(payload.Expressions.ElementAt(0));
                        return ConvertPayloadTag($"<{originalPayloadTag}>{expression1}</{originalPayloadTag}>");
                    }

                    return ConvertPayloadTag($"<{payloadTag}({string.Join( ',', payload.Expressions.Select(
                        ex => ConvertExpression(ex)
                    ) )})>");
                }
            }
    }
}