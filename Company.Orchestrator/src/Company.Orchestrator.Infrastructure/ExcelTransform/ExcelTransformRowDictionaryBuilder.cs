using ClosedXML.Excel;

namespace Company.Orchestrator.Infrastructure.ExcelTransform;

internal static class ExcelTransformRowDictionaryBuilder
{
    public static Dictionary<string, string> Build(
        IXLWorksheet ws,
        int rowNumber,
        int headerRow,
        int lastColumn)
    {
        var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var col = 1; col <= lastColumn; col++)
        {
            var letter = XLHelper.GetColumnLetterFromNumber(col);
            var value  = GetCellValueAsString(ws.Cell(rowNumber, col));
            row[letter] = value;

            if (headerRow >= 1)
            {
                var header = GetCellValueAsString(ws.Cell(headerRow, col)).Trim();
                if (!string.IsNullOrEmpty(header))
                    row[header] = value;
            }
        }

        return row;
    }

    public static Dictionary<string, object> BuildVariables(
        IReadOnlyDictionary<string, object> workflowVariables)
    {
        var variables = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in workflowVariables)
            variables[key] = value;
        return variables;
    }

    private static string GetCellValueAsString(IXLCell cell)
    {
        if (cell.Value.IsBlank) return string.Empty;
        if (cell.Value.IsText) return cell.GetString();
        if (cell.Value.IsNumber)
            return cell.Value.GetNumber().ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (cell.Value.IsDateTime)
            return cell.Value.GetDateTime().ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (cell.Value.IsBoolean)
            return cell.Value.GetBoolean().ToString();
        return cell.GetString();
    }
}
