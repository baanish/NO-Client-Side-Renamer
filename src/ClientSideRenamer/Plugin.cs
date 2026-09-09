using System.Globalization;
using BepInEx;
using BepInEx.Logging;
using ClientSideRenamer.Aliases;
using ClientSideRenamer.Remote;
using HarmonyLib;

namespace ClientSideRenamer;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInProcess("NuclearOption.exe")]
[BepInDependency(ConfigurationManagerGuid, BepInDependency.DependencyFlags.SoftDependency)]
internal sealed class Plugin : BaseUnityPlugin
{
    internal const string PluginGuid = "com.baanish.nuclearoption.clientsiderenamer";
    internal const string PluginName = "Nuclear Option Client-Side Renamer";
    internal const string PluginVersion = "0.2.1";
    private const string ConfigurationManagerGuid = "com.bepis.bepinex.configurationmanager";

    private static Plugin _instance;

    private readonly ReloadWatcher _watcher = new();
    private readonly HashSet<ulong> _fallbackWarnings = new();
    private readonly RemoteRosterSet _remote = new(PluginConfig.MaximumRemoteRosters);

    private PluginConfig _settings;
    private AliasFileStore _aliases;
    private AliasEditor _editor;
    private Harmony _harmony;
    private bool _reconfigurePending;
    private bool _watcherRebindPending;
    private bool _refreshPending;
    private readonly bool[] _remoteReconfigurePending = new bool[PluginConfig.MaximumRemoteRosters];

    internal new ManualLogSource Logger => base.Logger;
    internal int AliasCount => _aliases?.Current.Count ?? 0;
    internal string AliasFilePath => _aliases?.FilePath ?? "[not loaded]";
    internal string EditorStatus { get; private set; } = string.Empty;
    internal IEnumerable<string> RemoteStatuses
    {
        get
        {
            for (var index = 0; index < _remote.Count; index++)
            {
                var source = _settings.RemoteRosters[index];
                if (source.Enabled.Value || !string.IsNullOrWhiteSpace(source.Url.Value))
                {
                    yield return $"Roster {index + 1}: {_remote[index].Status}";
                }
            }
        }
    }

    private void Awake()
    {
        _instance = this;

        _editor = new AliasEditor(this);
        _settings = new PluginConfig(Config, _editor.Draw);
        _settings.Enabled.SettingChanged += OnEnabledChanged;
        _settings.AliasFile.SettingChanged += OnAliasFileChanged;
        _settings.ReloadOnChange.SettingChanged += OnReloadOnChangeChanged;
        foreach (var source in _settings.RemoteRosters)
        {
            source.Enabled.SettingChanged += OnRemoteSettingChanged;
        }

        ReconfigureAliasFile();

        try
        {
            _harmony = new Harmony(PluginGuid);
            _harmony.PatchAll();
            for (var index = 0; index < _remote.Count; index++)
            {
                RefreshRemote(index);
            }
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded with {AliasCount} aliases.");
        }
        catch (Exception exception)
        {
            enabled = false;
            Logger.LogError($"Failed to patch the player-name resolver: {exception}");
        }
    }

    private void Update()
    {
        for (var index = 0; index < _remote.Count; index++)
        {
            var roster = _remote[index];
            if (_remoteReconfigurePending[index])
            {
                _remoteReconfigurePending[index] = false;
                roster.Clear();
                _refreshPending = true;
                _editor.RefreshUneditedInputs();
                RefreshRemote(index);
            }

            if (roster.Poll())
            {
                Logger.LogInfo($"Roster {index + 1}: {roster.Status}");
                _editor.RefreshUneditedInputs();
                _refreshPending = true;
            }
        }

        if (_reconfigurePending)
        {
            _reconfigurePending = false;
            ReconfigureAliasFile();
        }

        if (_watcherRebindPending)
        {
            _watcherRebindPending = false;
            RebindWatcher();
        }

        if (_watcher.Poll())
        {
            ReloadAliases("File change");
        }

        if (_refreshPending)
        {
            _refreshPending = false;
            try
            {
                PlayerNameCache.RefreshAll();
            }
            catch (Exception exception)
            {
                Logger.LogError($"Failed to refresh player-name caches: {exception}");
            }
        }
    }

    private void OnDestroy()
    {
        if (_settings != null)
        {
            _settings.Enabled.SettingChanged -= OnEnabledChanged;
            _settings.AliasFile.SettingChanged -= OnAliasFileChanged;
            _settings.ReloadOnChange.SettingChanged -= OnReloadOnChangeChanged;
            foreach (var source in _settings.RemoteRosters)
            {
                source.Enabled.SettingChanged -= OnRemoteSettingChanged;
            }
        }

        _watcher.Dispose();
        _remote.Dispose();
        _harmony?.UnpatchSelf();

        if (_instance == this)
        {
            _instance = null;
        }
    }

    internal static bool TryResolveAlias(ulong steamId, string steamName, out string displayName)
    {
        displayName = string.Empty;
        var instance = _instance;
        if (instance == null || instance._settings == null || !instance._settings.Enabled.Value)
        {
            return false;
        }

        return instance.TryResolve(steamId, steamName, logFallback: true, out displayName);
    }

    internal bool TryGetAlias(ulong steamId, string steamName, out string displayName)
    {
        return TryResolve(steamId, steamName, logFallback: false, out displayName);
    }

    internal void ReloadFromEditor()
    {
        ReloadAliases("Manual reload");
        for (var index = 0; index < _remote.Count; index++)
        {
            if (!_remoteReconfigurePending[index])
            {
                RefreshRemote(index);
            }
        }
    }

    internal void SaveAlias(ulong steamId, string steamName, string displayName)
    {
        if (_aliases == null)
        {
            EditorStatus = "Alias file is not loaded.";
            return;
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            EditorStatus = "Alias must not be empty.";
            return;
        }

        var id = steamId.ToString(CultureInfo.InvariantCulture);
        var document = _aliases.GetDocument();
        var entry = document.Players.FirstOrDefault(player => player.SteamId == id);

        if (entry == null && !string.IsNullOrEmpty(steamName))
        {
            entry = document.Players.FirstOrDefault(
                player => string.IsNullOrEmpty(player.SteamId) && player.SteamName == steamName
            );
        }

        if (entry == null)
        {
            entry = new PlayerAliasEntry();
            document.Players.Add(entry);
        }

        entry.Enabled = true;
        entry.SteamId = id;
        entry.SteamName = steamName ?? string.Empty;
        entry.DisplayName = displayName;

        document.Players.RemoveAll(
            player => player != entry
                && string.IsNullOrEmpty(player.SteamId)
                && !string.IsNullOrEmpty(steamName)
                && player.SteamName == steamName
        );

        var result = _aliases.Save(document);
        if (!result.Succeeded)
        {
            EditorStatus = result.Error;
            Logger.LogWarning(result.Error);
            return;
        }

        _fallbackWarnings.Remove(steamId);
        EditorStatus = $"Saved alias for {steamName} ({id}).";
        _editor.ClearInputs();
        _refreshPending = true;
    }

    internal bool RemoveAlias(ulong steamId, string steamName)
    {
        if (_aliases == null)
        {
            EditorStatus = "Alias file is not loaded.";
            return false;
        }

        var id = steamId.ToString(CultureInfo.InvariantCulture);
        var document = _aliases.GetDocument();
        var removed = document.Players.RemoveAll(
            player => player.SteamId == id
                || string.IsNullOrEmpty(player.SteamId) && player.SteamName == steamName
        );

        if (removed == 0)
        {
            EditorStatus = $"No alias is saved for {steamName} ({id}).";
            return false;
        }

        var result = _aliases.Save(document);
        if (!result.Succeeded)
        {
            EditorStatus = result.Error;
            Logger.LogWarning(result.Error);
            return false;
        }

        _fallbackWarnings.Remove(steamId);
        EditorStatus = $"Removed local alias for {steamName} ({id}). Remote aliases still apply when enabled.";
        _editor.ClearInputs();
        _refreshPending = true;
        return true;
    }

    internal void LogEditorException(Exception exception)
    {
        Logger.LogError($"Alias editor error: {exception}");
    }

    private bool TryResolve(ulong steamId, string steamName, bool logFallback, out string displayName)
    {
        displayName = string.Empty;
        if (steamId == 0)
        {
            return false;
        }

        var id = steamId.ToString(CultureInfo.InvariantCulture);
        var local = _aliases?.Current ?? AliasSnapshot.Empty;
        if (local.TryResolve(id, null, out displayName))
        {
            return true;
        }

        if (!local.TryResolve(string.Empty, steamName, out displayName))
        {
            return _remote.TryResolve(id, steamName, out displayName);
        }

        if (logFallback && _fallbackWarnings.Add(steamId))
        {
            Logger.LogWarning(
                $"Matched Steam name '{steamName}' by fallback. Its SteamID64 is {id}; save it in F1 to make the mapping stable."
            );
        }

        return true;
    }

    private void ReconfigureAliasFile()
    {
        try
        {
            var path = ResolveAliasFilePath(_settings.AliasFile.Value);
            _aliases = new AliasFileStore(path);
            _fallbackWarnings.Clear();
            ReloadAliases("Configuration");
            RebindWatcher();
        }
        catch (Exception exception)
        {
            _aliases = null;
            _watcher.TryBind(string.Empty, false, out _);
            EditorStatus = $"Alias file configuration failed: {exception.Message}";
            Logger.LogError(EditorStatus);
            _refreshPending = true;
        }
    }

    private void RebindWatcher()
    {
        if (_watcher.TryBind(
            AliasFilePath,
            _aliases != null && _settings.ReloadOnChange.Value,
            out var error))
        {
            return;
        }

        EditorStatus = $"Alias file watching failed: {error}";
        Logger.LogError(EditorStatus);
    }

    private void ReloadAliases(string reason)
    {
        if (_aliases == null)
        {
            EditorStatus = "Alias file is not loaded.";
            return;
        }

        var result = _aliases.Reload();
        if (!result.Succeeded)
        {
            EditorStatus = result.RetainedLastKnownGood
                ? $"Reload failed; retained the last valid aliases. {result.Error}"
                : $"Reload failed. {result.Error}";
            Logger.LogWarning(EditorStatus);
            return;
        }

        EditorStatus = result.CreatedFile
            ? $"Created and loaded {_aliases.FilePath}."
            : $"Loaded {AliasCount} aliases.";
        Logger.LogInfo($"{reason}: {EditorStatus}");
        _fallbackWarnings.Clear();
        _editor?.ClearInputs();
        _refreshPending = true;
    }

    private static string ResolveAliasFilePath(string configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            throw new ArgumentException("Aliases.File must not be empty.");
        }

        return Path.GetFullPath(
            Path.IsPathRooted(configuredPath)
                ? configuredPath
                : Path.Combine(Paths.ConfigPath, configuredPath)
        );
    }

    private void OnEnabledChanged(object sender, EventArgs eventArgs)
    {
        _refreshPending = true;
        Array.Fill(_remoteReconfigurePending, true);
    }

    private void OnRemoteSettingChanged(object sender, EventArgs eventArgs)
    {
        for (var index = 0; index < _settings.RemoteRosters.Count; index++)
        {
            if (ReferenceEquals(sender, _settings.RemoteRosters[index].Enabled))
            {
                _remoteReconfigurePending[index] = true;
                break;
            }
        }
    }

    private void RefreshRemote(int index)
    {
        var source = _settings.RemoteRosters[index];
        if (!_settings.Enabled.Value || !source.Enabled.Value)
        {
            return;
        }

        var endpoint = source.Url.Value;
        var password = source.Password.Value;
        _remote[index].Refresh(async token =>
        {
            using var client = new RemoteCsvClient();
            return await client.FetchAsync(endpoint, password, token).ConfigureAwait(false);
        });
    }

    private void OnAliasFileChanged(object sender, EventArgs eventArgs)
    {
        _reconfigurePending = true;
    }

    private void OnReloadOnChangeChanged(object sender, EventArgs eventArgs)
    {
        _watcherRebindPending = true;
    }
}
