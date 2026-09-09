using System.Diagnostics;
using ClientSideRenamer.Aliases;
using ClientSideRenamer.Remote;
using Xunit;

namespace ClientSideRenamer.Tests;

public sealed class RemoteRosterSetTests
{
    private const string FirstSteamId = "76561198000000001";
    private const string SecondSteamId = "76561198000000002";

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void Roster_count_is_bounded(int count)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RemoteRosterSet(count));
    }

    [Fact]
    public async Task Earlier_slot_wins_conflicts_even_when_it_finishes_last()
    {
        using var rosters = new RemoteRosterSet(2);
        var firstResponse = new TaskCompletionSource<AliasSnapshot>();
        var secondResponse = new TaskCompletionSource<AliasSnapshot>();
        rosters[0].Refresh(_ => firstResponse.Task);
        rosters[1].Refresh(_ => secondResponse.Task);

        secondResponse.SetResult(Snapshot(FirstSteamId, "Second roster"));
        await PollUntilCompleted(rosters[1]);
        Assert.True(rosters.TryResolve(FirstSteamId, out var firstResolved));
        Assert.Equal("Second roster", firstResolved);

        firstResponse.SetResult(Snapshot(FirstSteamId, "First roster"));
        await PollUntilCompleted(rosters[0]);

        Assert.Equal(2, rosters.Count);
        Assert.True(rosters.TryResolve(FirstSteamId, out var finalResolved));
        Assert.Equal("First roster", finalResolved);
    }

    [Fact]
    public async Task Clearing_the_first_slot_immediately_uses_the_next_matching_roster()
    {
        using var rosters = new RemoteRosterSet(2);
        rosters[0].Refresh(_ => Task.FromResult(Snapshot(FirstSteamId, "First roster")));
        rosters[1].Refresh(_ => Task.FromResult(Snapshot(FirstSteamId, "Second roster")));
        await PollUntilCompleted(rosters[0]);
        await PollUntilCompleted(rosters[1]);

        rosters[0].Clear();

        Assert.True(rosters.TryResolve(FirstSteamId, out var resolved));
        Assert.Equal("Second roster", resolved);
    }

    [Fact]
    public async Task Failed_refresh_retains_its_source_while_another_source_updates()
    {
        using var rosters = new RemoteRosterSet(2);
        rosters[0].Refresh(_ => Task.FromResult(Snapshot(FirstSteamId, "Retained pilot")));
        rosters[1].Refresh(_ => Task.FromResult(Snapshot(SecondSteamId, "Old pilot")));
        await PollUntilCompleted(rosters[0]);
        await PollUntilCompleted(rosters[1]);
        var failedResponse = new TaskCompletionSource<AliasSnapshot>();
        var successfulResponse = new TaskCompletionSource<AliasSnapshot>();
        rosters[0].Refresh(_ => failedResponse.Task);
        rosters[1].Refresh(_ => successfulResponse.Task);

        successfulResponse.SetResult(Snapshot(SecondSteamId, "Updated pilot"));
        await PollUntilCompleted(rosters[1]);
        failedResponse.SetException(new IOException("Untrusted server error"));
        await PollUntilCompleted(rosters[0]);

        Assert.True(rosters.TryResolve(FirstSteamId, out var retained));
        Assert.Equal("Retained pilot", retained);
        Assert.True(rosters.TryResolve(SecondSteamId, out var updated));
        Assert.Equal("Updated pilot", updated);
    }

    [Fact]
    public async Task Empty_first_source_falls_back_to_the_next_matching_roster()
    {
        using var rosters = new RemoteRosterSet(2);
        rosters[0].Refresh(_ => Task.FromResult(Snapshot(FirstSteamId, "First roster")));
        rosters[1].Refresh(_ => Task.FromResult(Snapshot(FirstSteamId, "Second roster")));
        await PollUntilCompleted(rosters[0]);
        await PollUntilCompleted(rosters[1]);
        var response = new TaskCompletionSource<AliasSnapshot>();
        rosters[0].Refresh(_ => response.Task);

        response.SetResult(AliasSnapshot.Empty);
        await PollUntilCompleted(rosters[0]);

        Assert.True(rosters.TryResolve(FirstSteamId, out var resolved));
        Assert.Equal("Second roster", resolved);
    }

    [Fact]
    public async Task Unique_aliases_from_each_source_are_available_together()
    {
        using var rosters = new RemoteRosterSet(2);
        var firstResponse = new TaskCompletionSource<AliasSnapshot>();
        var secondResponse = new TaskCompletionSource<AliasSnapshot>();
        rosters[0].Refresh(_ => firstResponse.Task);
        rosters[1].Refresh(_ => secondResponse.Task);

        firstResponse.SetResult(Snapshot(FirstSteamId, "First pilot"));
        secondResponse.SetResult(Snapshot(SecondSteamId, "Second pilot"));
        await PollUntilCompleted(rosters[0]);
        await PollUntilCompleted(rosters[1]);

        Assert.True(rosters.TryResolve(FirstSteamId, out var first));
        Assert.Equal("First pilot", first);
        Assert.True(rosters.TryResolve(SecondSteamId, out var second));
        Assert.Equal("Second pilot", second);
        Assert.False(rosters.TryResolve("76561198000000003", out var missing));
        Assert.Equal(string.Empty, missing);
    }

    private static AliasSnapshot Snapshot(string steamId, string displayName) => new(
        new Dictionary<string, string> { [steamId] = displayName },
        new Dictionary<string, string>());

    private static async Task PollUntilCompleted(RemoteRoster roster)
    {
        var elapsed = Stopwatch.StartNew();
        while (!roster.Poll())
        {
            Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(5), "Remote fetch did not complete within five seconds.");
            await Task.Delay(1);
        }
    }
}
