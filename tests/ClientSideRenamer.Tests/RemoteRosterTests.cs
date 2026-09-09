using System.Diagnostics;
using ClientSideRenamer.Aliases;
using ClientSideRenamer.Remote;
using Xunit;

namespace ClientSideRenamer.Tests;

public sealed class RemoteRosterTests
{
    [Fact]
    public async Task Fetch_returns_before_completion_and_only_poll_applies_the_snapshot()
    {
        using var roster = new RemoteRoster();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<AliasSnapshot>();
        var expected = Snapshot("Remote pilot");

        roster.Refresh(_ =>
        {
            started.SetResult();
            return response.Task;
        });
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Same(AliasSnapshot.Empty, roster.Current);
        Assert.False(roster.Poll());
        response.SetResult(expected);
        Assert.Same(AliasSnapshot.Empty, roster.Current);

        await PollUntilCompleted(roster);

        Assert.Same(expected, roster.Current);
        Assert.Equal("Loaded 1 remote aliases.", roster.Status);
        Assert.False(roster.Poll());
    }

    [Fact]
    public async Task Failed_refresh_retains_known_good_aliases_and_hides_exception_details()
    {
        using var roster = new RemoteRoster();
        var expected = Snapshot("Known pilot");
        roster.Refresh(_ => Task.FromResult(expected));
        await PollUntilCompleted(roster);
        var response = new TaskCompletionSource<AliasSnapshot>();

        roster.Refresh(_ => response.Task);
        response.SetException(new InvalidOperationException("https://private.example/?PASSWORD=secret"));
        await PollUntilCompleted(roster);

        Assert.Same(expected, roster.Current);
        Assert.Equal(
            "Remote CSV fetch failed (InvalidOperationException); check HTTPS endpoint, authentication and CSV format. "
                + "Retained 1 remote aliases.",
            roster.Status);
        Assert.False(roster.Poll());
    }

    [Fact]
    public async Task Clear_cancels_the_request_and_discards_a_late_success_that_ignores_cancellation()
    {
        using var roster = new RemoteRoster();
        roster.Refresh(_ => Task.FromResult(Snapshot("Previous pilot")));
        await PollUntilCompleted(roster);
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var response = new TaskCompletionSource<AliasSnapshot>();
        roster.Refresh(token =>
        {
            started.SetResult(token);
            return response.Task;
        });
        var cancellation = await started.Task.WaitAsync(TimeSpan.FromSeconds(5));

        roster.Clear();
        response.SetResult(Snapshot("Stale pilot"));

        Assert.True(cancellation.IsCancellationRequested);
        Assert.False(SpinWait.SpinUntil(roster.Poll, TimeSpan.FromMilliseconds(250)));
        Assert.Same(AliasSnapshot.Empty, roster.Current);
        Assert.Equal("Remote CSV is disabled or awaiting configuration.", roster.Status);
    }

    [Fact]
    public async Task Replacement_fetch_cancels_the_old_request_and_ignores_its_late_failure()
    {
        using var roster = new RemoteRoster();
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldResponse = new TaskCompletionSource<AliasSnapshot>();
        roster.Refresh(token =>
        {
            started.SetResult(token);
            return oldResponse.Task;
        });
        var oldCancellation = await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var replacement = new TaskCompletionSource<AliasSnapshot>();
        var expected = Snapshot("Replacement pilot");

        roster.Refresh(_ => replacement.Task);
        replacement.SetResult(expected);
        await PollUntilCompleted(roster);
        oldResponse.SetException(new InvalidOperationException("https://stale.example/?PASSWORD=old-secret"));

        Assert.True(oldCancellation.IsCancellationRequested);
        Assert.False(SpinWait.SpinUntil(roster.Poll, TimeSpan.FromMilliseconds(250)));
        Assert.Same(expected, roster.Current);
        Assert.Equal("Loaded 1 remote aliases.", roster.Status);
    }

    [Fact]
    public async Task Valid_empty_response_removes_the_previous_remote_roster()
    {
        using var roster = new RemoteRoster();
        roster.Refresh(_ => Task.FromResult(Snapshot("Previous pilot")));
        await PollUntilCompleted(roster);

        roster.Refresh(_ => Task.FromResult(AliasSnapshot.Empty));
        await PollUntilCompleted(roster);

        Assert.Same(AliasSnapshot.Empty, roster.Current);
        Assert.Equal("Loaded 0 remote aliases.", roster.Status);
    }

    private static AliasSnapshot Snapshot(string displayName) => new(
        new Dictionary<string, string> { ["76561198000000001"] = displayName },
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
