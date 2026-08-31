using System;
using System.IO;
using Xunit;

namespace ClientSideRenamer.Tests;

public sealed class ReloadWatcherTests
{
    [Fact]
    public void Missing_directory_is_reported_without_throwing()
    {
        using var temp = new TemporaryDirectory();
        using var watcher = new ReloadWatcher();
        var path = Path.Combine(temp.Path, "missing", "aliases.json");

        var succeeded = watcher.TryBind(path, enabled: true, out var error);

        Assert.False(succeeded);
        Assert.NotEmpty(error);
        Assert.False(watcher.Poll());
    }

    [Fact]
    public void Disabled_watcher_does_not_require_a_valid_path()
    {
        using var watcher = new ReloadWatcher();

        var succeeded = watcher.TryBind(string.Empty, enabled: false, out var error);

        Assert.True(succeeded);
        Assert.Empty(error);
    }

    [Fact]
    public void Watcher_can_be_disabled_and_reenabled()
    {
        using var temp = new TemporaryDirectory();
        var path = Path.Combine(temp.Path, "aliases.json");
        File.WriteAllText(path, "{}");
        using var watcher = new ReloadWatcher();

        Assert.True(watcher.TryBind(path, enabled: true, out var firstError), firstError);
        Assert.True(watcher.TryBind(path, enabled: false, out var disabledError), disabledError);
        Assert.True(watcher.TryBind(path, enabled: true, out var secondError), secondError);
    }

    [Fact]
    public void Watcher_can_recover_after_a_missing_directory_is_created()
    {
        using var temp = new TemporaryDirectory();
        var directory = Path.Combine(temp.Path, "missing");
        var path = Path.Combine(directory, "aliases.json");
        using var watcher = new ReloadWatcher();

        Assert.False(watcher.TryBind(path, enabled: true, out _));
        Directory.CreateDirectory(directory);
        File.WriteAllText(path, "{}");

        Assert.True(watcher.TryBind(path, enabled: true, out var error), error);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"ClientSideRenamer-Watcher-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
