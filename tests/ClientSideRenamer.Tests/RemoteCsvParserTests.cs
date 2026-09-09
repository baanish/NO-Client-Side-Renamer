using System.Text;
using ClientSideRenamer.Remote;
using Xunit;

namespace ClientSideRenamer.Tests;

public sealed class RemoteCsvParserTests
{
    private const string SteamId = "76561198000000001";

    [Theory]
    [InlineData("PlayerName,SteamID\n\"Chaza, \"\"Wyvern\"\"\",76561198000000001\n")]
    [InlineData("\uFEFFsteamid,playername\r\n76561198000000001,\"Chaza, \"\"Wyvern\"\"\"")]
    public void Parses_explicit_headers_and_escaped_csv_fields(string csv)
    {
        var snapshot = Parse(csv);
        Assert.Equal(1, snapshot.Count);
        Assert.True(snapshot.TryResolve(SteamId, "Original", out var name));
        Assert.Equal("Chaza, \"Wyvern\"", name);
        Assert.False(snapshot.TryResolve(string.Empty, "Chaza, \"Wyvern\"", out _));
    }

    [Theory]
    [InlineData("PlayerName,SteamID")]
    [InlineData("SteamID,PlayerName\r\n")]
    public void Header_only_csv_is_an_empty_roster(string csv) => Assert.Equal(0, Parse(csv).Count);

    [Theory]
    [InlineData("")]
    [InlineData("Name,SteamID\nPilot,76561198000000001")]
    [InlineData("PlayerName,PlayerName")]
    [InlineData("PlayerName,SteamID,Extra")]
    [InlineData("PlayerName,SteamID\nPilot,76561198000000001,Extra")]
    [InlineData("PlayerName,SteamID\nPilot")]
    [InlineData("PlayerName,SteamID\n\"Pilot,76561198000000001")]
    [InlineData("PlayerName,SteamID\n\"Pilot\"x,76561198000000001")]
    [InlineData("PlayerName,SteamID\nPi\"lot,76561198000000001")]
    [InlineData("PlayerName,SteamID\n\"Pi\nlot\",76561198000000001")]
    [InlineData("PlayerName,SteamID\n\"Pi\rlot\",76561198000000001")]
    [InlineData("PlayerName,SteamID\n\n")]
    public void Rejects_malformed_or_ambiguous_csv(string csv) => Assert.Throws<FormatException>(() => Parse(csv));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("<b>Pilot</b>")]
    [InlineData("Pilot>")]
    [InlineData("Pilot\tName")]
    [InlineData("Pilot\0Name")]
    [InlineData("Pilot\u202EName")]
    [InlineData("Pilot\u200BName")]
    [InlineData("Pilot\u2028Name")]
    [InlineData("Pilot\u2029Name")]
    public void Rejects_empty_control_format_and_rich_text_names(string name) =>
        Assert.Throws<FormatException>(() => Parse($"PlayerName,SteamID\n{name},{SteamId}"));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("<b>baanish</b>")]
    [InlineData("baanish\tName")]
    [InlineData("baanish\0Name")]
    [InlineData("baanish\u202EName")]
    [InlineData("baanish\u200BName")]
    [InlineData("baanish\u2028Name")]
    [InlineData("baanish\u2029Name")]
    public void Rejects_unsafe_original_steam_names(string name) =>
        Assert.Throws<FormatException>(() => Parse($"PlayerName,SteamID\nPilot,{name}"));

    [Theory]
    [InlineData("0")]
    [InlineData("76561197960265728")]
    [InlineData("76561202255233024")]
    [InlineData("076561198000000001")]
    public void Rejects_noncanonical_or_nonindividual_ids(string id) =>
        Assert.Throws<FormatException>(() => Parse($"PlayerName,SteamID\nPilot,{id}"));

    [Theory]
    [InlineData("baanish")]
    [InlineData("+76561198000000001")]
    [InlineData(" 76561198000000001")]
    [InlineData("76561198000000001 ")]
    [InlineData("１２３")]
    [InlineData("飞行员 🛩️")]
    public void Nondigit_identifiers_resolve_only_as_exact_original_names(string originalName)
    {
        var snapshot = Parse($"PlayerName,SteamID\nPilot,{originalName}");
        Assert.True(snapshot.TryResolve(SteamId, originalName, out var alias));
        Assert.Equal("Pilot", alias);
        Assert.False(snapshot.TryResolve(SteamId, "Original", out _));
        Assert.False(snapshot.TryResolve(originalName, null!, out _));
    }

    [Fact]
    public void Name_keys_preserve_case_whitespace_and_csv_escaping()
    {
        var snapshot = Parse("SteamID,PlayerName\nbaanish,First\nBaanish,Second\n\" baanish, \"\"Pilot\"\" \",Third");
        Assert.Equal(3, snapshot.Count);
        Assert.True(snapshot.TryResolve(SteamId, "baanish", out var first));
        Assert.Equal("First", first);
        Assert.True(snapshot.TryResolve(SteamId, "Baanish", out var second));
        Assert.Equal("Second", second);
        Assert.True(snapshot.TryResolve(SteamId, " baanish, \"Pilot\" ", out var third));
        Assert.Equal("Third", third);
        Assert.False(snapshot.TryResolve(SteamId, "BAANISH", out _));
        Assert.False(snapshot.TryResolve(SteamId, "baanish, \"Pilot\"", out _));
    }

    [Fact]
    public void Id_wins_over_name_within_a_mixed_roster()
    {
        var snapshot = Parse($"PlayerName,SteamID\nBy name,baanish\nBy ID,{SteamId}");
        Assert.Equal(2, snapshot.Count);
        Assert.True(snapshot.TryResolve(SteamId, "baanish", out var alias));
        Assert.Equal("By ID", alias);
        Assert.True(snapshot.TryResolve("76561198000000002", "baanish", out alias));
        Assert.Equal("By name", alias);
    }

    [Fact]
    public void Duplicate_exact_name_rejects_the_entire_mixed_roster()
    {
        Assert.Throws<FormatException>(() => Parse($"PlayerName,SteamID\nID,{SteamId}\nFirst,baanish\nSecond,baanish"));
    }

    [Fact]
    public void Duplicate_id_rejects_the_entire_roster()
    {
        var error = Assert.Throws<FormatException>(() => Parse($"PlayerName,SteamID\nFirst,{SteamId}\nSecretName,{SteamId}"));
        Assert.DoesNotContain("SecretName", error.ToString());
        Assert.DoesNotContain(SteamId, error.ToString());
    }

    [Fact]
    public void Name_length_is_bounded_and_unicode_remains_supported()
    {
        Assert.Equal(1, Parse($"PlayerName,SteamID\n{new string('a', 128)},{SteamId}").Count);
        Assert.Throws<FormatException>(() => Parse($"PlayerName,SteamID\n{new string('a', 129)},{SteamId}"));
        Assert.Equal(1, Parse($"PlayerName,SteamID\nPilot,{new string('a', 128)}").Count);
        Assert.Throws<FormatException>(() => Parse($"PlayerName,SteamID\nPilot,{new string('a', 129)}"));
        var snapshot = Parse($"PlayerName,SteamID\n飞行员 🛩️,{SteamId}");
        Assert.True(snapshot.TryResolve(SteamId, "", out var name));
        Assert.Equal("飞行员 🛩️", name);
    }

    [Fact]
    public void Rejects_invalid_utf8_instead_of_replacing_bytes()
    {
        var bytes = Encoding.UTF8.GetBytes($"PlayerName,SteamID\nPilot,{SteamId}");
        bytes[20] = 0xff;
        Assert.Throws<FormatException>(() => RemoteCsvParser.Parse(bytes));
    }

    [Fact]
    public void Bounds_total_bytes_and_rows()
    {
        Assert.Equal(1048576, RemoteCsvParser.MaximumBytes);
        Assert.Equal(10000, RemoteCsvParser.MaximumRows);
        Assert.Equal(128, RemoteCsvParser.MaximumNameLength);
        Assert.Throws<FormatException>(() => RemoteCsvParser.Parse(new byte[1048577]));
        var csv = new StringBuilder("PlayerName,SteamID\n");
        for (var index = 1; index <= 10000; index++)
            csv.Append("Pilot,").Append(76561197960265728UL + (ulong)index).Append('\n');
        Assert.Equal(10000, Parse(csv.ToString()).Count);
        csv.Append("Pilot,76561198000000001");
        Assert.Throws<FormatException>(() => Parse(csv.ToString()));
    }

    [Fact]
    public void Mixed_ids_and_names_share_the_row_limit()
    {
        var csv = new StringBuilder("PlayerName,SteamID\n");
        for (var index = 1; index <= 5000; index++)
        {
            csv.Append("Pilot,").Append(76561197960265728UL + (ulong)index).Append('\n');
            csv.Append("Pilot,Name").Append(index).Append('\n');
        }
        Assert.Equal(10000, Parse(csv.ToString()).Count);
        csv.Append("Pilot,baanish");
        Assert.Throws<FormatException>(() => Parse(csv.ToString()));
    }

    private static ClientSideRenamer.Aliases.AliasSnapshot Parse(string csv) =>
        RemoteCsvParser.Parse(Encoding.UTF8.GetBytes(csv));
}
