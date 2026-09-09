using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using ClientSideRenamer.Aliases;

namespace ClientSideRenamer.Remote;

internal static class RemoteCsvParser
{
    internal const int MaximumBytes = 1024 * 1024;
    internal const int MaximumRows = 10000;
    internal const int MaximumNameLength = 128;

    internal static AliasSnapshot Parse(byte[] bytes)
    {
        if (bytes == null || bytes.Length > MaximumBytes)
            throw InvalidCsv();

        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw InvalidCsv();
        }

        var position = text.Length > 0 && text[0] == '\uFEFF' ? 1 : 0;
        if (position == text.Length)
            throw InvalidCsv();

        var header = ReadRow(text, ref position);
        var nameColumn = string.Equals(header[0], "PlayerName", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
        if (!string.Equals(header[nameColumn], "PlayerName", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(header[1 - nameColumn], "SteamID", StringComparison.OrdinalIgnoreCase))
            throw InvalidCsv();

        var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
        while (position < text.Length)
        {
            if (aliases.Count >= MaximumRows)
                throw InvalidCsv();

            var row = ReadRow(text, ref position);
            var name = row[nameColumn];
            var steamId = row[1 - nameColumn];
            if (string.IsNullOrWhiteSpace(name) || !AliasFileValidator.IsCanonicalSteamId64(steamId))
                throw InvalidCsv();

            for (var index = 0; index < name.Length; index++)
            {
                var category = CharUnicodeInfo.GetUnicodeCategory(name, index);
                if (category == UnicodeCategory.Control || category == UnicodeCategory.Format
                    || category == UnicodeCategory.LineSeparator || category == UnicodeCategory.ParagraphSeparator
                    || name[index] == '<' || name[index] == '>')
                    throw InvalidCsv();
            }

            if (aliases.ContainsKey(steamId))
                throw InvalidCsv();
            aliases.Add(steamId, name);
        }

        return new AliasSnapshot(aliases, new Dictionary<string, string>(StringComparer.Ordinal));
    }

    private static string[] ReadRow(string text, ref int position)
    {
        var fields = new string[2];
        for (var column = 0; column < fields.Length; column++)
        {
            var field = new StringBuilder();
            var quoted = position < text.Length && text[position] == '"';
            if (quoted)
                position++;
            var closed = !quoted;
            while (position < text.Length)
            {
                var character = text[position];
                if (character == '\r' || character == '\n')
                {
                    if (quoted && !closed)
                        throw InvalidCsv();
                    break;
                }
                if (character == '"')
                {
                    if (!quoted)
                        throw InvalidCsv();
                    position++;
                    if (position < text.Length && text[position] == '"')
                        position++;
                    else
                    {
                        closed = true;
                        break;
                    }
                }
                else
                {
                    if (!quoted && character == ',')
                        break;
                    position++;
                }

                if (field.Length >= MaximumNameLength)
                    throw InvalidCsv();
                field.Append(character);
            }
            if (!closed)
                throw InvalidCsv();
            fields[column] = field.ToString();

            if (column == 0)
            {
                if (position >= text.Length || text[position] != ',')
                    throw InvalidCsv();
                position++;
            }
            else if (position < text.Length)
            {
                if (text[position] != '\r' && text[position] != '\n')
                    throw InvalidCsv();
                if (text[position++] == '\r' && position < text.Length && text[position] == '\n')
                    position++;
            }
        }
        return fields;
    }

    private static FormatException InvalidCsv() => new("Remote CSV is invalid or exceeds the supported limits.");
}
