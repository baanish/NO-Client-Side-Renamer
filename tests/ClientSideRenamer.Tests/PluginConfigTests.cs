using BepInEx.Configuration;
using Xunit;

namespace ClientSideRenamer.Tests;

public sealed class PluginConfigTests
{
    [Fact]
    public void Eight_independent_slots_keep_the_original_section_first()
    {
        var settings = new PluginConfig(NewConfig(), _ => { });

        Assert.Equal(8, PluginConfig.MaximumRemoteRosters);
        Assert.Equal(8, settings.RemoteRosters.Count);
        for (var index = 0; index < settings.RemoteRosters.Count; index++)
        {
            var roster = settings.RemoteRosters[index];
            var section = index == 0 ? "Remote CSV" : $"Remote CSV {index + 1}";
            Assert.Equal(section, roster.Section);
            Assert.Equal(section, roster.Enabled.Definition.Section);
            Assert.Equal(section, roster.Url.Definition.Section);
            Assert.Equal(section, roster.Password.Definition.Section);
            Assert.False(roster.Enabled.Value);
            Assert.Empty(roster.Url.Value);
            Assert.Empty(roster.Password.Value);
        }
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(0, false)]
    [InlineData(3, true)]
    [InlineData(3, false)]
    [InlineData(7, true)]
    [InlineData(7, false)]
    public void Editing_a_source_disables_only_its_slot_until_explicitly_enabled(int changedIndex, bool changeUrl)
    {
        var settings = new PluginConfig(NewConfig(), _ => { });
        for (var index = 0; index < settings.RemoteRosters.Count; index++)
        {
            var roster = settings.RemoteRosters[index];
            roster.Url.Value = $"https://roster{index}.example/players.csv";
            roster.Password.Value = $"token-{index}";
            roster.Enabled.Value = true;
        }

        var changed = settings.RemoteRosters[changedIndex];
        if (changeUrl)
        {
            changed.Url.Value = "https://unfinished.example/players.csv";
        }
        else
        {
            changed.Password.Value = "unfinished-token";
        }

        for (var index = 0; index < settings.RemoteRosters.Count; index++)
        {
            var roster = settings.RemoteRosters[index];
            Assert.Equal(index != changedIndex, roster.Enabled.Value);
            Assert.Equal(index == changedIndex && changeUrl ? "https://unfinished.example/players.csv" : $"https://roster{index}.example/players.csv", roster.Url.Value);
            Assert.Equal(index == changedIndex && !changeUrl ? "unfinished-token" : $"token-{index}", roster.Password.Value);
        }
        changed.Enabled.Value = true;
        Assert.All(settings.RemoteRosters, roster => Assert.True(roster.Enabled.Value));
    }

    [Fact]
    public void Existing_single_roster_entries_bind_to_the_first_slot_without_migration()
    {
        var config = NewConfig();
        var enabled = config.Bind("Remote CSV", "Enabled", true);
        var url = config.Bind("Remote CSV", "Url", "https://roster.example/players.csv");
        var password = config.Bind("Remote CSV", "Password", "saved-token");

        var settings = new PluginConfig(config, _ => { });
        var first = settings.RemoteRosters[0];

        Assert.Same(enabled, first.Enabled);
        Assert.Same(url, first.Url);
        Assert.Same(password, first.Password);
        Assert.True(first.Enabled.Value);
        Assert.Equal("https://roster.example/players.csv", first.Url.Value);
        Assert.Equal("saved-token", first.Password.Value);
        Assert.All(settings.RemoteRosters.Skip(1), roster => Assert.False(roster.Enabled.Value));
    }

    [Fact]
    public void Saved_enabled_states_and_credentials_survive_binding_on_mod_load()
    {
        var config = NewConfig();
        try
        {
            var original = new PluginConfig(config, _ => { });
            for (var index = 0; index < original.RemoteRosters.Count; index++)
            {
                var roster = original.RemoteRosters[index];
                roster.Url.Value = $"https://roster{index}.example/players.csv";
                roster.Password.Value = $"saved-token-{index}";
                roster.Enabled.Value = index % 2 == 0;
            }
            config.Save();

            var loadedConfig = new ConfigFile(config.ConfigFilePath, false) { SaveOnConfigSet = false };
            var loaded = new PluginConfig(loadedConfig, _ => { });

            for (var index = 0; index < loaded.RemoteRosters.Count; index++)
            {
                var roster = loaded.RemoteRosters[index];
                Assert.Equal(index % 2 == 0, roster.Enabled.Value);
                Assert.Equal($"https://roster{index}.example/players.csv", roster.Url.Value);
                Assert.Equal($"saved-token-{index}", roster.Password.Value);
            }
        }
        finally
        {
            File.Delete(config.ConfigFilePath);
        }
    }

    private static ConfigFile NewConfig() => new(
        Path.Combine(Path.GetTempPath(), $"ClientSideRenamer-tests-{Guid.NewGuid():N}.cfg"), false)
    {
        SaveOnConfigSet = false
    };
}
