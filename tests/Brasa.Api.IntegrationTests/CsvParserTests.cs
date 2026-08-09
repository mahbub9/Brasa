using Brasa.Api.Csv;

namespace Brasa.Api.IntegrationTests;

/// <summary>
/// CAT-17 — <see cref="CsvParser"/> is a hand-written RFC 4180 reader (no
/// third-party dependency for something this small and well-specified), so
/// its edge cases (quoting, escaped quotes, line endings, blank lines) are
/// worth pinning directly rather than only exercising through the import
/// endpoint's E2E tests.
/// </summary>
/// <remarks>Pure in-memory parsing, no DB — runs as fast as a unit test despite living in the integration-test project.</remarks>
public class CsvParserTests
{
    [Fact]
    public void Parses_a_simple_two_row_csv()
    {
        var rows = CsvParser.Parse("a,b,c\n1,2,3\n");

        rows.ShouldBe(new[]
        {
            new[] { "a", "b", "c" },
            new[] { "1", "2", "3" },
        });
    }

    [Fact]
    public void Handles_a_missing_trailing_newline()
    {
        var rows = CsvParser.Parse("a,b\nc,d");

        rows.ShouldBe(new[]
        {
            new[] { "a", "b" },
            new[] { "c", "d" },
        });
    }

    [Fact]
    public void Handles_crlf_line_endings()
    {
        var rows = CsvParser.Parse("a,b\r\nc,d\r\n");

        rows.ShouldBe(new[]
        {
            new[] { "a", "b" },
            new[] { "c", "d" },
        });
    }

    [Fact]
    public void Skips_blank_lines_without_producing_an_empty_row()
    {
        var rows = CsvParser.Parse("a,b\n\nc,d\n");

        rows.ShouldBe(new[]
        {
            new[] { "a", "b" },
            new[] { "c", "d" },
        });
    }

    [Fact]
    public void A_quoted_field_may_contain_a_comma()
    {
        var rows = CsvParser.Parse("\"Bife, grelhado\",c\n");

        rows.ShouldBe(new[] { new[] { "Bife, grelhado", "c" } });
    }

    [Fact]
    public void A_quoted_field_may_contain_an_escaped_quote()
    {
        var rows = CsvParser.Parse("\"a\"\"b\",c\n");

        rows.ShouldBe(new[] { new[] { "a\"b", "c" } });
    }

    [Fact]
    public void A_quoted_field_may_contain_a_newline()
    {
        var rows = CsvParser.Parse("\"line1\nline2\",b\n");

        rows.ShouldBe(new[] { new[] { "line1\nline2", "b" } });
    }

    [Fact]
    public void An_empty_string_produces_no_rows()
    {
        CsvParser.Parse(string.Empty).ShouldBeEmpty();
    }
}
