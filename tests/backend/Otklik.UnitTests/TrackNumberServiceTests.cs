using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Otklik.Infrastructure.Security;
using Xunit;

namespace Otklik.UnitTests;

public sealed partial class TrackNumberServiceTests
{
    private readonly TrackNumberService service = new(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Security:TrackHashKey"] = "b3RrbGlrLWxvY2FsLXRyYWNrLWhtYWMta2V5LTIwMjYtMzJieXRlcw=="
        })
        .Build());

    [Fact]
    public void Generate_uses_expected_format_without_ambiguous_symbols()
    {
        var values = Enumerable.Range(0, 100).Select(_ => service.Generate()).ToArray();

        Assert.All(values, value => Assert.Matches(TrackPattern(), value));
        Assert.Equal(values.Length, values.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void TryNormalize_accepts_case_spaces_and_common_dashes()
    {
        var success = service.TryNormalize("  отк–ABCD efgh  ", out var normalized);

        Assert.True(success);
        Assert.Equal("ОТК-ABCD-EFGH", normalized);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ОТК-ABIO-EFGH")]
    [InlineData("ОТК-ABC-EFGH")]
    [InlineData("ABC-ABCD-EFGH")]
    public void TryNormalize_rejects_invalid_or_ambiguous_values(string? value)
    {
        Assert.False(service.TryNormalize(value, out _));
    }

    [GeneratedRegex("^ОТК-[23456789ABCDEFGHJKMNPQRSTUVWXYZ]{4}-[23456789ABCDEFGHJKMNPQRSTUVWXYZ]{4}$")]
    private static partial Regex TrackPattern();
}
