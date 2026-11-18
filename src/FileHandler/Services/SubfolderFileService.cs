using System.IO;
using System.Security;
using FileHandler.Localization;
using FileHandler.Models;

namespace FileHandler.Services;

public sealed class SubfolderFileService
{
    private readonly FileTreeScanner _scanner = new();
    private readonly string _logPath;

    public string LogPath => _logPath;

    public SubfolderFileService(string? logPath = null)
    {
        _logPath = logPath ?? ActivityLog.DefaultPath;
    }

    public SubfolderFileScanResult Scan(string source, IProgress<OperationProgress> progress)
    {
        var sourcePath = Path.GetFullPath(source);
        if (!Directory.Exists(sourcePath))
        {
            throw new DirectoryNotFoundException(Text.Format("SourceFolderNotFound", sourcePath));
        }

        using var log = new ActivityLog(LogPath);
        log.Write(Text.Format("StartingSubfolderScan", sourcePath));
        var scan = _scanner.Scan(sourcePath, excludedPath: null, progress, log.Write);
        var files = scan.Files
            .Where(file => !PathsEqual(Path.GetDirectoryName(file)!, sourcePath))
            .ToArray();

        log.Write(
            Text.Format(
                "SubfolderScanCompletedLog",
                files.Length,
                scan.SubdirectoryCount,
                scan.Errors));
        progress.Report(new OperationProgress(
            files.Length,
            files.Length,
            Text.Format("SubfolderScanCompleted", files.Length)));

        return new SubfolderFileScanResult(sourcePath, files, scan.Errors);
    }

    public SubfolderFileSummary Move(
        SubfolderFileScanResult scan,
        string destination,
        IProgress<OperationProgress> progress)
    {
        if (scan.Files.Count == 0)
        {
            throw new InvalidOperationException(Text.Get("NoSubfolderFilesToMove"));
        }

        var destinationPath = Path.GetFullPath(destination);
        using var log = new ActivityLog(LogPath);
        log.Write(
            Text.Format("StartingSubfolderMove", scan.Files.Count, destinationPath));

        try
        {
            Directory.CreateDirectory(destinationPath);
        }
        catch (IOException exception)
        {
            log.Write(Text.Format("CannotPrepareDestination", exception.Message));
            throw;
        }
        catch (UnauthorizedAccessException exception)
        {
            log.Write(Text.Format("NoPermissionPreparingDestination", exception.Message));
            throw;
        }
        catch (SecurityException exception)
        {
            log.Write(Text.Format("AccessDeniedPreparingDestination", exception.Message));
            throw;
        }

        var moved = 0;
        var collisionRenames = 0;
        var alreadyAtDestination = 0;
        var errors = 0;
        var total = scan.Files.Count;
        var processed = 0;

        foreach (var sourceFile in scan.Files)
        {
            if (PathsEqual(Path.GetDirectoryName(sourceFile)!, destinationPath))
            {
                alreadyAtDestination++;
                var existingMessage = Text.Format("FileAlreadyAtDestinationRoot", sourceFile);
                log.Write(existingMessage);
                progress.Report(new OperationProgress(++processed, total, existingMessage));
                continue;
            }

            try
            {
                var fileName = Path.GetFileName(sourceFile);
                var desiredPath = Path.Combine(destinationPath, fileName);
                var hasCollision = File.Exists(desiredPath) || Directory.Exists(desiredPath);
                var target = UniqueDestinationPath.GetAvailablePath(destinationPath, fileName);
                File.Move(sourceFile, target);
                moved++;
                if (hasCollision)
                {
                    collisionRenames++;
                    log.Write(Text.Format("CollisionRenamedIncoming", target));
                }

                var movedMessage = Text.Format("FileMoved", sourceFile, target);
                log.Write(movedMessage);
                progress.Report(new OperationProgress(++processed, total, movedMessage));
            }
            catch (IOException exception)
            {
                errors++;
                var errorMessage = Text.Format("CannotMoveFile", sourceFile, exception.Message);
                log.Write(errorMessage);
                progress.Report(new OperationProgress(++processed, total, errorMessage));
            }
            catch (UnauthorizedAccessException exception)
            {
                errors++;
                var errorMessage = Text.Format("NoPermissionMovingFile", sourceFile, exception.Message);
                log.Write(errorMessage);
                progress.Report(new OperationProgress(++processed, total, errorMessage));
            }
            catch (SecurityException exception)
            {
                errors++;
                var errorMessage = Text.Format("AccessDeniedMovingFile", sourceFile, exception.Message);
                log.Write(errorMessage);
                progress.Report(new OperationProgress(++processed, total, errorMessage));
            }
        }

        var summary = new SubfolderFileSummary(
            total,
            moved,
            collisionRenames,
            alreadyAtDestination,
            errors,
            scan.ScanErrors);
        log.Write(
            Text.Format(
                "SubfolderMoveCompletedLog",
                summary.FilesFound,
                summary.Moved,
                summary.CollisionRenames,
                summary.AlreadyAtDestination,
                summary.ScanErrors,
                summary.Errors));
        progress.Report(new OperationProgress(
            total,
            total,
            Text.Format(
                "SubfolderMoveCompleted",
                summary.Moved,
                summary.AlreadyAtDestination,
                summary.Errors)));
        return summary;
    }

    private static bool PathsEqual(string first, string second) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(first),
            Path.TrimEndingDirectorySeparator(second),
            StringComparison.OrdinalIgnoreCase);
}
