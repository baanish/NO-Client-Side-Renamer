using System;
using System.IO;
using System.Linq;
using ClientSideRenamer.Aliases;
using Xunit;

namespace ClientSideRenamer.Tests;

public sealed class AliasFileStoreTests
{
    [Fact]
    public void Reload_creates_initial_file_and_parent_directory()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "nested", "aliases.json");
        var store = new AliasFileStore(path);

        var result = store.Reload();

        Assert.True(result.Succeeded);
        Assert.True(result.CreatedFile);
        Assert.True(File.Exists(path));
        Assert.True(store.Current.TryResolve(string.Empty, "Baanish", out var alias));
        Assert.Equal("Baanish | Reaper 5-2", alias);
        Assert.False(store.Current.TryResolve(string.Empty, "Example", out _));
    }

    [Fact]
    public void Reload_does_not_replace_a_malformed_existing_file()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "aliases.json");
        File.WriteAllText(path, "{ broken");
        var store = new AliasFileStore(path);

        var result = store.Reload();

        Assert.False(result.Succeeded);
        Assert.False(result.CreatedFile);
        Assert.False(result.RetainedLastKnownGood);
        Assert.False(store.IsDiskValid);
        Assert.Equal("{ broken", File.ReadAllText(path));
        Assert.False(store.Save(store.GetDocument()).Succeeded);
    }

    [Fact]
    public void Get_document_returns_a_detached_copy()
    {
        using var temp = new TemporaryDirectory();
        var store = CreateLoadedStore(temp);
        var document = store.GetDocument();

        document.Players.Single(player => player.SteamName == "Baanish").DisplayName = "Unsaved";

        Assert.True(store.Current.TryResolve(string.Empty, "Baanish", out var alias));
        Assert.Equal("Baanish | Reaper 5-2", alias);
        Assert.DoesNotContain("Unsaved", File.ReadAllText(store.FilePath));
    }

    [Fact]
    public void Steam_id_entries_match_only_their_id()
    {
        using var temp = new TemporaryDirectory();
        var store = CreateLoadedStore(temp);
        var document = store.GetDocument();
        document.Players.Clear();
        document.Players.Add(Entry("76561198000000001", "Baanish", "Reaper"));
        Assert.True(store.Save(document).Succeeded);

        Assert.True(store.Current.TryResolve("76561198000000001", "Someone Else", out var alias));
        Assert.Equal("Reaper", alias);
        Assert.False(store.Current.TryResolve("76561198000000002", "Baanish", out _));
    }

    [Fact]
    public void Name_fallback_is_exact_and_case_sensitive()
    {
        using var temp = new TemporaryDirectory();
        var store = CreateLoadedStore(temp);

        Assert.True(store.Current.TryResolve("76561198000000001", "Baanish", out _));
        Assert.False(store.Current.TryResolve("76561198000000001", "baanish", out _));
    }

    [Theory]
    [InlineData("duplicate-id")]
    [InlineData("disabled-duplicate-id")]
    [InlineData("duplicate-name")]
    [InlineData("disabled-duplicate-name")]
    [InlineData("invalid-id")]
    [InlineData("empty-alias")]
    [InlineData("unsupported-schema")]
    public void Invalid_documents_are_rejected_without_changing_the_current_mapping(string scenario)
    {
        using var temp = new TemporaryDirectory();
        var store = CreateLoadedStore(temp);
        var document = ValidDocument();

        switch (scenario)
        {
            case "duplicate-id":
                document.Players.Add(Entry("76561198000000001", "Other", "Two"));
                break;
            case "disabled-duplicate-id":
                document.Players.Add(Entry("76561198000000001", "Other", "Two", enabled: false));
                break;
            case "duplicate-name":
                document.Players.Add(Entry(string.Empty, "Baanish", "Two"));
                break;
            case "disabled-duplicate-name":
                document.Players.Add(Entry(string.Empty, "Baanish", "Two", enabled: false));
                break;
            case "invalid-id":
                document.Players[0].SteamId = "not-an-id";
                break;
            case "empty-alias":
                document.Players[0].DisplayName = " ";
                break;
            case "unsupported-schema":
                document.SchemaVersion = 2;
                break;
        }

        var result = store.Save(document);

        Assert.False(result.Succeeded);
        Assert.True(store.IsDiskValid);
        Assert.True(store.Current.TryResolve(string.Empty, "Baanish", out var alias));
        Assert.Equal("Baanish | Reaper 5-2", alias);
    }

    [Fact]
    public void Malformed_reload_retains_last_known_good_and_blocks_save()
    {
        using var temp = new TemporaryDirectory();
        var store = CreateLoadedStore(temp);
        File.WriteAllText(store.FilePath, "{ broken");

        var reload = store.Reload();

        Assert.False(reload.Succeeded);
        Assert.True(reload.RetainedLastKnownGood);
        Assert.False(store.IsDiskValid);
        Assert.True(store.Current.TryResolve(string.Empty, "Baanish", out _));
        Assert.False(store.Save(store.GetDocument()).Succeeded);
        Assert.Equal("{ broken", File.ReadAllText(store.FilePath));
    }

    [Fact]
    public void Invalid_reload_retains_last_known_good_and_blocks_save()
    {
        using var temp = new TemporaryDirectory();
        var store = CreateLoadedStore(temp);
        File.WriteAllText(store.FilePath, "{\"schemaVersion\":2,\"players\":[]}");

        var reload = store.Reload();

        Assert.False(reload.Succeeded);
        Assert.True(reload.RetainedLastKnownGood);
        Assert.Contains("Unsupported schemaVersion", reload.Error);
        Assert.False(store.IsDiskValid);
        Assert.True(store.Current.TryResolve(string.Empty, "Baanish", out _));
        Assert.False(store.Save(store.GetDocument()).Succeeded);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("18446744073709551615")]
    [InlineData(" 76561198000000001")]
    [InlineData("076561198000000001")]
    [InlineData(" ")]
    public void Noncanonical_steam_ids_are_rejected(string steamId)
    {
        using var temp = new TemporaryDirectory();
        var store = CreateLoadedStore(temp);
        var document = ValidDocument();
        document.Players[0].SteamId = steamId;

        var result = store.Save(document);

        Assert.False(result.Succeeded);
        Assert.Contains("valid SteamID64", result.Error);
    }

    [Fact]
    public void Save_checks_for_external_disk_corruption_before_overwriting()
    {
        using var temp = new TemporaryDirectory();
        var store = CreateLoadedStore(temp);
        var document = store.GetDocument();
        document.Players.Single(player => player.SteamName == "Baanish").DisplayName = "Changed";
        File.WriteAllText(store.FilePath, "[]");

        var result = store.Save(document);

        Assert.False(result.Succeeded);
        Assert.False(store.IsDiskValid);
        Assert.Equal("[]", File.ReadAllText(store.FilePath));
        Assert.True(store.Current.TryResolve(string.Empty, "Baanish", out var alias));
        Assert.Equal("Baanish | Reaper 5-2", alias);
    }

    [Fact]
    public void Successful_save_replaces_the_document_without_leaving_temp_files()
    {
        using var temp = new TemporaryDirectory();
        var store = CreateLoadedStore(temp);
        var document = ValidDocument();

        var result = store.Save(document);

        Assert.True(result.Succeeded);
        Assert.True(store.Current.TryResolve("76561198000000001", "ignored", out var alias));
        Assert.Equal("One", alias);
        Assert.Empty(Directory.GetFiles(temp.Path, "*.tmp", SearchOption.AllDirectories));
    }

    private static AliasFileStore CreateLoadedStore(TemporaryDirectory temp)
    {
        var store = new AliasFileStore(Path.Combine(temp.Path, "aliases.json"));
        Assert.True(store.Reload().Succeeded);
        return store;
    }

    private static AliasFileDocument ValidDocument()
    {
        return new AliasFileDocument
        {
            Players =
            {
                Entry("76561198000000001", "Original", "One"),
                Entry(string.Empty, "Baanish", "Two")
            }
        };
    }

    private static PlayerAliasEntry Entry(
        string steamId,
        string steamName,
        string displayName,
        bool enabled = true)
    {
        return new PlayerAliasEntry
        {
            Enabled = enabled,
            SteamId = steamId,
            SteamName = steamName,
            DisplayName = displayName
        };
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ClientSideRenamer-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
