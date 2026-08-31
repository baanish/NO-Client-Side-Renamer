using System.Linq;
using Xunit;

namespace ClientSideRenamer.Tests;

public sealed class PlayerPickerTests
{
    [Fact]
    public void Empty_roster_has_no_selection()
    {
        var options = PlayerPicker.Normalize(Array.Empty<PlayerPickerCandidate>());

        Assert.Empty(options);
        Assert.Equal(-1, PlayerPicker.ResolveSelectionIndex(options, 123));
    }

    [Fact]
    public void One_player_is_selected_by_default()
    {
        var options = PlayerPicker.Normalize(new[] { Candidate(0, 10, 4, "Pilot") });

        Assert.Equal(0, PlayerPicker.ResolveSelectionIndex(options, 0));
        Assert.Equal((ulong)10, options[0].SteamId);
    }

    [Fact]
    public void One_hundred_players_are_sorted_and_searchable_without_a_result_cap()
    {
        var candidates = Enumerable.Range(0, 100)
            .Reverse()
            .Select(index => Candidate(index, (ulong)(1000 + index), index, $"Pilot {index:D3}"));

        var options = PlayerPicker.Normalize(candidates);
        var byName = PlayerPicker.Filter(options, "pilot 042");
        var bySteamId = PlayerPicker.Filter(options, "1042");

        Assert.Equal(100, options.Length);
        Assert.Equal(0, options[0].PlayerIndex);
        Assert.Equal(99, options[^1].PlayerIndex);
        Assert.Equal(8, PlayerPicker.MaxVisibleRows);
        Assert.Single(byName);
        Assert.Equal((ulong)1042, byName[0].SteamId);
        Assert.Single(bySteamId);
        Assert.Equal((ulong)1042, bySteamId[0].SteamId);
    }

    [Fact]
    public void Duplicate_ids_choose_the_lowest_player_index_deterministically()
    {
        var options = PlayerPicker.Normalize(new[]
        {
            Candidate(0, 50, 8, "Later"),
            Candidate(1, 50, 2, "Earlier"),
            Candidate(2, 60, 3, "Other")
        });

        Assert.Equal(2, options.Length);
        Assert.Equal((ulong)50, options[0].SteamId);
        Assert.Equal("Earlier", options[0].OriginalName);
        Assert.Equal(1, options[0].SourceIndex);
    }

    [Fact]
    public void Selection_survives_reordering_and_falls_back_when_removed()
    {
        var reordered = PlayerPicker.Normalize(new[]
        {
            Candidate(0, 20, 1, "Two"),
            Candidate(1, 10, 0, "One")
        });
        var removed = PlayerPicker.Normalize(new[] { Candidate(0, 10, 0, "One") });

        Assert.Equal(1, PlayerPicker.ResolveSelectionIndex(reordered, 20));
        Assert.Equal(0, PlayerPicker.ResolveSelectionIndex(removed, 20));
    }

    [Fact]
    public void Long_unicode_names_are_truncated_only_for_display()
    {
        var longName = string.Concat(Enumerable.Repeat("Pilot 🚀 ", 20));
        var option = PlayerPicker.Normalize(new[] { Candidate(0, 123456, 0, longName) })[0];

        Assert.EndsWith("... [3456]", option.VisibleLabel);
        Assert.Contains(longName.Trim(), option.FullLabel);
        Assert.True(option.Matches("Pilot 🚀 Pilot"));
    }

    private static PlayerPickerCandidate Candidate(
        int sourceIndex,
        ulong steamId,
        int playerIndex,
        string name)
    {
        return new PlayerPickerCandidate(sourceIndex, steamId, playerIndex, name);
    }
}
