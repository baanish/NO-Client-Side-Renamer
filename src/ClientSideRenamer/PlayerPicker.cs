using System.Globalization;

namespace ClientSideRenamer;

internal readonly struct PlayerPickerCandidate
{
    public PlayerPickerCandidate(int sourceIndex, ulong steamId, int playerIndex, string originalName)
    {
        SourceIndex = sourceIndex;
        SteamId = steamId;
        PlayerIndex = playerIndex;
        OriginalName = originalName ?? string.Empty;
    }

    public int SourceIndex { get; }
    public ulong SteamId { get; }
    public int PlayerIndex { get; }
    public string OriginalName { get; }
}

internal readonly struct PlayerPickerOption
{
    private const int MaxVisibleNameElements = 42;

    public PlayerPickerOption(PlayerPickerCandidate candidate)
    {
        SourceIndex = candidate.SourceIndex;
        SteamId = candidate.SteamId;
        PlayerIndex = candidate.PlayerIndex;
        OriginalName = candidate.OriginalName;

        var name = SanitizeName(candidate.OriginalName);
        if (string.IsNullOrEmpty(name))
        {
            name = "[name pending]";
        }

        var suffix = $" [{candidate.SteamId % 10000:D4}]";
        FullLabel = name + suffix;
        VisibleLabel = Truncate(name, MaxVisibleNameElements) + suffix;
        SearchText = name + "\n" + candidate.SteamId.ToString(CultureInfo.InvariantCulture);
    }

    public int SourceIndex { get; }
    public ulong SteamId { get; }
    public int PlayerIndex { get; }
    public string OriginalName { get; }
    public string FullLabel { get; }
    public string VisibleLabel { get; }
    private string SearchText { get; }

    public bool Matches(string query)
    {
        var trimmed = query?.Trim();
        return string.IsNullOrEmpty(trimmed)
            || SearchText.IndexOf(trimmed, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string SanitizeName(string value)
    {
        return (value ?? string.Empty)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('\t', ' ');
    }

    private static string Truncate(string value, int maxTextElements)
    {
        var text = new StringInfo(value);
        if (text.LengthInTextElements <= maxTextElements)
        {
            return value;
        }

        return text.SubstringByTextElements(0, maxTextElements - 3) + "...";
    }
}

internal static class PlayerPicker
{
    public const int MaxVisibleRows = 8;

    public static PlayerPickerOption[] Normalize(IEnumerable<PlayerPickerCandidate> candidates)
    {
        return candidates
            .Where(candidate => candidate.SteamId != 0)
            .OrderBy(candidate => candidate.PlayerIndex)
            .ThenBy(candidate => candidate.SteamId)
            .ThenBy(candidate => candidate.OriginalName, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.SourceIndex)
            .GroupBy(candidate => candidate.SteamId)
            .Select(group => new PlayerPickerOption(group.First()))
            .ToArray();
    }

    public static PlayerPickerOption[] Filter(
        IReadOnlyList<PlayerPickerOption> options,
        string query)
    {
        return options.Where(option => option.Matches(query)).ToArray();
    }

    public static int ResolveSelectionIndex(
        IReadOnlyList<PlayerPickerOption> options,
        ulong selectedSteamId)
    {
        for (var index = 0; index < options.Count; index++)
        {
            if (options[index].SteamId == selectedSteamId)
            {
                return index;
            }
        }

        return options.Count == 0 ? -1 : 0;
    }
}
