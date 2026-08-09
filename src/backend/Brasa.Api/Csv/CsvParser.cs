using System.Text;

namespace Brasa.Api.Csv;

/// <summary>
/// A minimal RFC 4180 CSV reader — comma-separated fields, double-quote
/// wrapping with <c>""</c> as an escaped quote, and both CRLF and LF line
/// endings. No third-party dependency for something this small and this
/// well-specified (CAT-17).
/// </summary>
public static class CsvParser
{
    /// <summary>Parses <paramref name="csv"/> into rows of fields. Blank lines are skipped, never returned as an empty row.</summary>
    public static IReadOnlyList<IReadOnlyList<string>> Parse(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);

        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var rowHasContent = false;

        void EndField()
        {
            row.Add(field.ToString());
            field.Clear();
        }

        void EndRow()
        {
            EndField();
            if (rowHasContent || row.Count > 1)
            {
                rows.Add(row);
            }

            row = [];
            rowHasContent = false;
        }

        for (var i = 0; i < csv.Length; i++)
        {
            var c = csv[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    field.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    inQuotes = true;
                    rowHasContent = true;
                    break;
                case ',':
                    EndField();
                    rowHasContent = true;
                    break;
                case '\r':
                    break;
                case '\n':
                    EndRow();
                    break;
                default:
                    field.Append(c);
                    rowHasContent = true;
                    break;
            }
        }

        // The last row has no trailing newline to trigger EndRow above.
        if (field.Length > 0 || rowHasContent || row.Count > 0)
        {
            EndRow();
        }

        return rows;
    }
}
