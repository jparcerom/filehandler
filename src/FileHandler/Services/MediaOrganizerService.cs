using System.IO;
using System.Security;
using System.Security.Cryptography;
using FileHandler.Localization;
using FileHandler.Models;

namespace FileHandler.Services;

public sealed class MediaOrganizerService
{
    private readonly MediaCopyService _mediaScanner;
    private readonly string _logPath;

    public string LogPath => _logPath;

    public MediaOrganizerService(string? logPath = null)
    {
        _logPath = logPath ?? ActivityLog.DefaultPath;
        _mediaScanner = new MediaCopyService(_logPath);
    }

    public MediaScanResult Scan(string source, IProgress<OperationProgress> progress) =>
        _mediaScanner.Scan(source, progress);

    public MediaOrganizationSummary Organize(
        MediaScanResult scan,
        bool organizeImages,
        string? imageDestination,
        bool organizeVideos,
        string? videoDestination,
        IProgress<OperationProgress> progress)
    {
        IReadOnlyList<string> images = organizeImages ? scan.ImageFiles : Array.Empty<string>();
        IReadOnlyList<string> videos = organizeVideos ? scan.VideoFiles : Array.Empty<string>();
        var total = images.Count + videos.Count;
        if (total == 0)
        {
            throw new InvalidOperationException(Text.Get("NoMediaSelectedToOrganize"));
        }

        var normalizedImageDestination = images.Count > 0
            ? ValidateDestination(scan.SourcePath, imageDestination, Text.Get("Images"))
            : null;
        var normalizedVideoDestination = videos.Count > 0
            ? ValidateDestination(scan.SourcePath, videoDestination, Text.Get("Videos"))
            : null;

        using var log = new ActivityLog(LogPath);
        log.Write(
            Text.Format(
                "StartingMediaOrganizationLog",
                images.Count,
                videos.Count,
                normalizedImageDestination ?? Text.Get("NotSelected"),
                normalizedVideoDestination ?? Text.Get("NotSelected")));

        try
        {
            if (normalizedImageDestination is not null)
            {
                Directory.CreateDirectory(normalizedImageDestination);
            }
            if (normalizedVideoDestination is not null &&
                !PathsEqual(normalizedVideoDestination, normalizedImageDestination))
            {
                Directory.CreateDirectory(normalizedVideoDestination);
            }
        }
        catch (IOException exception)
        {
            log.Write(Text.Format("CannotPrepareDestinations", exception.Message));
            throw;
        }
        catch (UnauthorizedAccessException exception)
        {
            log.Write(Text.Format("NoPermissionPreparingDestinations", exception.Message));
            throw;
        }
        catch (SecurityException exception)
        {
            log.Write(Text.Format("AccessDeniedPreparingDestinations", exception.Message));
            throw;
        }

        var files = new List<OrganizationFile>(total);
        files.AddRange(images.Select(file => new OrganizationFile(file, true, normalizedImageDestination!)));
        files.AddRange(videos.Select(file => new OrganizationFile(file, false, normalizedVideoDestination!)));

        var imagesMoved = 0;
        var videosMoved = 0;
        var collisionRenames = 0;
        var alreadyOrganized = 0;
        var errors = 0;

        for (var index = 0; index < files.Count; index++)
        {
            var file = files[index];
            try
            {
                if (!File.Exists(file.SourcePath))
                {
                    throw new FileNotFoundException(Text.Get("SourceFileNoLongerExists"), file.SourcePath);
                }

                var creationDate = File.GetCreationTime(file.SourcePath);
                var modifiedDate = File.GetLastWriteTime(file.SourcePath);
                var date = creationDate <= modifiedDate ? creationDate : modifiedDate;
                var targetDirectory = DateOrganizationPath.GetMonthDirectory(file.Destination, date);
                Directory.CreateDirectory(targetDirectory);

                var fileName = Path.GetFileName(file.SourcePath);
                var desiredPath = Path.Combine(targetDirectory, fileName);
                if (PathsEqual(file.SourcePath, desiredPath))
                {
                    alreadyOrganized++;
                    var existingMessage = Text.Format("FileAlreadyOrganized", file.SourcePath);
                    log.Write(existingMessage);
                    progress.Report(new OperationProgress(index + 1, total, existingMessage));
                    continue;
                }

                var targetPath = UniqueDestinationPath.GetAvailablePath(targetDirectory, fileName);
                var finalPath = MoveFileSafely(file.SourcePath, targetPath, targetDirectory, fileName, log.Write);
                if (!PathsEqual(finalPath, desiredPath))
                {
                    collisionRenames++;
                    log.Write(Text.Format("CollisionRenamedIncoming", finalPath));
                }

                if (file.IsImage)
                {
                    imagesMoved++;
                }
                else
                {
                    videosMoved++;
                }

                var movedMessage = Text.Format("FileMoved", file.SourcePath, finalPath);
                log.Write(movedMessage);
                progress.Report(new OperationProgress(index + 1, total, movedMessage));
            }
            catch (IOException exception)
            {
                errors++;
                var errorMessage = Text.Format("CannotOrganizeFile", file.SourcePath, exception.Message);
                log.Write(errorMessage);
                progress.Report(new OperationProgress(index + 1, total, errorMessage));
            }
            catch (UnauthorizedAccessException exception)
            {
                errors++;
                var errorMessage = Text.Format("NoPermissionOrganizingFile", file.SourcePath, exception.Message);
                log.Write(errorMessage);
                progress.Report(new OperationProgress(index + 1, total, errorMessage));
            }
            catch (SecurityException exception)
            {
                errors++;
                var errorMessage = Text.Format("AccessDeniedOrganizingFile", file.SourcePath, exception.Message);
                log.Write(errorMessage);
                progress.Report(new OperationProgress(index + 1, total, errorMessage));
            }
        }

        var summary = new MediaOrganizationSummary(
            images.Count,
            videos.Count,
            imagesMoved,
            videosMoved,
            collisionRenames,
            alreadyOrganized,
            errors,
            scan.Errors);
        log.Write(
            Text.Format(
                "MediaOrganizationCompletedLog",
                summary.ImagesMoved,
                summary.ImagesToOrganize,
                summary.VideosMoved,
                summary.VideosToOrganize,
                summary.CollisionRenames,
                summary.AlreadyOrganized,
                summary.ScanErrors,
                summary.Errors));
        progress.Report(new OperationProgress(
            total,
            total,
            Text.Format(
                "MediaOrganizationCompleted",
                summary.ImagesMoved,
                summary.VideosMoved,
                summary.CollisionRenames,
                summary.Errors)));
        return summary;
    }

    private static string ValidateDestination(string source, string? destination, string fileType)
    {
        if (string.IsNullOrWhiteSpace(destination))
        {
            throw new ArgumentException(Text.Format("SelectDestinationForType", fileType));
        }

        var destinationPath = Path.GetFullPath(destination);
        if (FileTreeScanner.IsSameOrDescendant(destinationPath, source))
        {
            throw new ArgumentException(Text.Format("DestinationCannotBeSourceOrInside", fileType));
        }

        return destinationPath;
    }

    private static string MoveFileSafely(
        string source,
        string destination,
        string destinationDirectory,
        string fileName,
        Action<string> log)
    {
        if (string.Equals(
                Path.GetPathRoot(source),
                Path.GetPathRoot(destination),
                StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                File.Move(source, destination);
                return destination;
            }
            catch (IOException exception)
            {
                log(Text.Format("SafeMoveRetry", source, exception.Message));
            }
        }
        else
        {
            log(Text.Format("CrossVolumeCopyWillBeVerified", source));
        }

        var fallbackDestination = UniqueDestinationPath.GetAvailablePath(destinationDirectory, fileName);
        try
        {
            File.Copy(source, fallbackDestination, overwrite: false);
        }
        catch (IOException exception)
        {
            throw new IOException(
                Text.Format("CopyFailedSourceRetained", fallbackDestination, exception.Message),
                exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new UnauthorizedAccessException(
                Text.Format("CopyFailedSourceRetained", fallbackDestination, exception.Message),
                exception);
        }
        catch (SecurityException exception)
        {
            throw new SecurityException(
                Text.Format("CopyFailedSourceRetained", fallbackDestination, exception.Message),
                exception);
        }

        string sourceHash;
        string destinationHash;
        try
        {
            sourceHash = ComputeSha256(source);
            destinationHash = ComputeSha256(fallbackDestination);
        }
        catch (IOException exception)
        {
            throw new IOException(
                Text.Format("CopyVerificationFailedSourceRetained", fallbackDestination, exception.Message),
                exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new UnauthorizedAccessException(
                Text.Format("CopyVerificationFailedSourceRetained", fallbackDestination, exception.Message),
                exception);
        }
        catch (SecurityException exception)
        {
            throw new SecurityException(
                Text.Format("CopyVerificationFailedSourceRetained", fallbackDestination, exception.Message),
                exception);
        }

        if (!string.Equals(sourceHash, destinationHash, StringComparison.Ordinal))
        {
            throw new IOException(
                Text.Format("CopyHashMismatchBothKept", source, fallbackDestination));
        }

        try
        {
            File.Delete(source);
        }
        catch (IOException exception)
        {
            throw new IOException(
                Text.Format("VerifiedCopySourceCouldNotBeRemoved", fallbackDestination, source, exception.Message),
                exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new UnauthorizedAccessException(
                Text.Format("VerifiedCopySourceCouldNotBeRemoved", fallbackDestination, source, exception.Message),
                exception);
        }
        catch (SecurityException exception)
        {
            throw new SecurityException(
                Text.Format("VerifiedCopySourceCouldNotBeRemoved", fallbackDestination, source, exception.Message),
                exception);
        }

        log(Text.Format("SourceDeletedAfterShaVerification", fallbackDestination));
        return fallbackDestination;
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static bool PathsEqual(string first, string? second) =>
        second is not null &&
        string.Equals(
            Path.TrimEndingDirectorySeparator(first),
            Path.TrimEndingDirectorySeparator(second),
            StringComparison.OrdinalIgnoreCase);

    private sealed record OrganizationFile(string SourcePath, bool IsImage, string Destination);
}
