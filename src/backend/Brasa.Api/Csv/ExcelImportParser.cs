using System.Globalization;
using System.Text;
using Brasa.Shared.Primitives;
using ExcelDataReader;

namespace Brasa.Api.Csv;

/// <summary>
/// Reads the first worksheet of an uploaded <c>.xlsx</c> file into the same
/// row/field shape <see cref="CsvParser"/> produces (CAT-17's Excel half),
/// so <c>CatalogEndpoints</c> can run both through one identical row-
/// processing pipeline. Uses ExcelDataReader directly rather than its
/// companion <c>ExcelDataReader.DataSet</c> package — a plain
/// <c>List&lt;List&lt;string&gt;&gt;</c> is all the shared pipeline needs, no
/// <c>DataSet</c>/<c>DataTable</c> machinery. Legacy <c>.xls</c> (BIFF) is
/// deliberately not supported — only <c>.xlsx</c> is accepted (<see cref="Validate"/>).
/// </summary>
public static class ExcelImportParser
{
    /// <summary>
    /// ExcelDataReader still probes Windows-1252 internally even for
    /// <c>.xlsx</c> (not just legacy BIFF), and .NET Core ships without it —
    /// every read throws <c>NotSupportedException: No data is available for
    /// encoding 1252</c> until a codepages provider is registered. Static
    /// constructor so it runs exactly once, before the first real upload
    /// hits this, not per-request.
    /// </summary>
    static ExcelImportParser()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    /// <summary>Rejects anything that isn't a non-empty <c>.xlsx</c> file, before any parsing is attempted.</summary>
    public static Result Validate(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            return Result.Failure(Error.Validation("catalog.import_empty", "The Excel file has no rows."));
        }

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(Error.Validation(
                "catalog.import_invalid_file", "Only .xlsx Excel files are supported."));
        }

        return Result.Success();
    }

    /// <summary>
    /// Parses the first worksheet. Throws if the file isn't a well-formed
    /// <c>.xlsx</c> package — the caller (<c>CatalogEndpoints</c>) turns
    /// that into <c>catalog.import_invalid_file</c> rather than a 500,
    /// since a corrupt upload is an expected failure at this boundary, not
    /// a programming error. A row with every cell blank is skipped, never
    /// returned as an empty row — the same convention <see cref="CsvParser"/>
    /// uses, needed here because Excel routinely carries trailing blank
    /// rows a spreadsheet user never notices.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>> Parse(Stream stream)
    {
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var rows = new List<IReadOnlyList<string>>();

        while (reader.Read())
        {
            var row = new List<string>(reader.FieldCount);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row.Add(Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? string.Empty);
            }

            if (row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            rows.Add(row);
        }

        return rows;
    }
}
