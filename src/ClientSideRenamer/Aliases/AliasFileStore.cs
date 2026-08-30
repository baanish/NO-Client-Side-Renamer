using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace ClientSideRenamer.Aliases;

public sealed class AliasFileStore
{
    private readonly string _filePath;
    private AliasFileDocument _document;

    public AliasFileStore(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("An alias file path is required.", nameof(filePath));
        }

        _filePath = Path.GetFullPath(filePath);
    }

    public string FilePath => _filePath;
    public AliasSnapshot Current { get; private set; } = AliasSnapshot.Empty;
    public bool HasLastKnownGood => _document != null;
    public bool IsDiskValid { get; private set; }
    public string DiskError { get; private set; } = "The alias file has not been loaded.";

    public AliasFileDocument GetDocument()
    {
        return (_document ?? AliasFileDocument.CreateInitialTemplate()).Copy();
    }

    public AliasLoadResult Reload()
    {
        var created = false;

        try
        {
            if (!File.Exists(_filePath))
            {
                created = TryCreateInitialFile();
            }

            var json = File.ReadAllText(_filePath, Encoding.UTF8);
            var document = JsonConvert.DeserializeObject<AliasFileDocument>(json);
            if (!AliasFileValidator.TryCreateSnapshot(document, out var snapshot, out var error))
            {
                return RetainLastKnownGood(error);
            }

            _document = document.Copy();
            Current = snapshot;
            IsDiskValid = true;
            DiskError = string.Empty;
            return new AliasLoadResult(true, created, false, string.Empty);
        }
        catch (JsonException exception)
        {
            return RetainLastKnownGood($"The alias file is malformed: {exception.Message}");
        }
        catch (IOException exception)
        {
            return RetainLastKnownGood($"The alias file could not be read: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            return RetainLastKnownGood($"The alias file could not be read: {exception.Message}");
        }
    }

    public AliasSaveResult Save(AliasFileDocument document)
    {
        if (!AliasFileValidator.TryCreateSnapshot(document, out var snapshot, out var proposedError))
        {
            return new AliasSaveResult(false, proposedError);
        }

        if (!IsDiskValid)
        {
            return new AliasSaveResult(false, $"Save refused because the alias file on disk is invalid: {DiskError}");
        }

        if (File.Exists(_filePath) && !TryValidateDisk(out var diskError))
        {
            IsDiskValid = false;
            DiskError = diskError;
            return new AliasSaveResult(false, $"Save refused because the alias file on disk is invalid: {diskError}");
        }

        try
        {
            WriteDocumentAtomic(document, replaceExisting: File.Exists(_filePath));
            _document = document.Copy();
            Current = snapshot;
            IsDiskValid = true;
            DiskError = string.Empty;
            return new AliasSaveResult(true, string.Empty);
        }
        catch (IOException exception)
        {
            return new AliasSaveResult(false, $"The alias file could not be saved: {exception.Message}");
        }
        catch (UnauthorizedAccessException exception)
        {
            return new AliasSaveResult(false, $"The alias file could not be saved: {exception.Message}");
        }
    }

    private bool TryCreateInitialFile()
    {
        var directory = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(directory);

        try
        {
            WriteDocumentAtomic(AliasFileDocument.CreateInitialTemplate(), replaceExisting: false);
            return true;
        }
        catch (IOException) when (File.Exists(_filePath))
        {
            return false;
        }
    }

    private bool TryValidateDisk(out string error)
    {
        try
        {
            var json = File.ReadAllText(_filePath, Encoding.UTF8);
            var document = JsonConvert.DeserializeObject<AliasFileDocument>(json);
            return AliasFileValidator.TryCreateSnapshot(document, out _, out error);
        }
        catch (JsonException exception)
        {
            error = $"The alias file is malformed: {exception.Message}";
            return false;
        }
        catch (IOException exception)
        {
            error = $"The alias file could not be read: {exception.Message}";
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            error = $"The alias file could not be read: {exception.Message}";
            return false;
        }
    }

    private void WriteDocumentAtomic(AliasFileDocument document, bool replaceExisting)
    {
        var directory = Path.GetDirectoryName(_filePath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(
            directory,
            $".{Path.GetFileName(_filePath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            var json = JsonConvert.SerializeObject(document, Formatting.Indented) + Environment.NewLine;
            var bytes = new UTF8Encoding(false).GetBytes(json);
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }

            if (replaceExisting)
            {
                File.Replace(temporaryPath, _filePath, null);
            }
            else
            {
                File.Move(temporaryPath, _filePath);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private AliasLoadResult RetainLastKnownGood(string error)
    {
        IsDiskValid = false;
        DiskError = error;
        return new AliasLoadResult(false, false, HasLastKnownGood, error);
    }
}

public sealed class AliasLoadResult
{
    internal AliasLoadResult(bool succeeded, bool createdFile, bool retainedLastKnownGood, string error)
    {
        Succeeded = succeeded;
        CreatedFile = createdFile;
        RetainedLastKnownGood = retainedLastKnownGood;
        Error = error;
    }

    public bool Succeeded { get; }
    public bool CreatedFile { get; }
    public bool RetainedLastKnownGood { get; }
    public string Error { get; }
}

public sealed class AliasSaveResult
{
    internal AliasSaveResult(bool succeeded, string error)
    {
        Succeeded = succeeded;
        Error = error;
    }

    public bool Succeeded { get; }
    public string Error { get; }
}
