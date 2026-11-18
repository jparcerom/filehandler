using System.IO;
using System.Security;
using FileHandler.Localization;
using FileHandler.Models;

namespace FileHandler.Services;

public sealed class FileTreeScanner
{
    public FileTreeScanResult Scan(
        string sourcePath,
        string? excludedPath,
        IProgress<OperationProgress>? progress,
        Action<string> log)
    {
        var files = new List<string>();
        var subdirectories = new List<string>();
        var errors = 0;
        var directories = new Stack<string>();
        directories.Push(sourcePath);

        while (directories.Count > 0)
        {
            var current = directories.Pop();
            progress?.Report(new OperationProgress(
                files.Count,
                0,
                Text.Format("ScanningFolderProgress", current, files.Count),
                IsScanning: true));

            try
            {
                files.AddRange(Directory.EnumerateFiles(current));
            }
            catch (IOException exception)
            {
                errors++;
                log(Text.Format("CannotEnumerateFolder", current, exception.Message));
            }
            catch (UnauthorizedAccessException exception)
            {
                errors++;
                log(Text.Format("NoPermissionEnumeratingFolder", current, exception.Message));
            }
            catch (SecurityException exception)
            {
                errors++;
                log(Text.Format("AccessDeniedEnumeratingFolder", current, exception.Message));
            }

            try
            {
                foreach (var subdirectory in Directory.EnumerateDirectories(current))
                {
                    if (excludedPath is not null && IsSameOrDescendant(subdirectory, excludedPath))
                    {
                        continue;
                    }

                    try
                    {
                        if ((File.GetAttributes(subdirectory) & FileAttributes.ReparsePoint) != 0)
                        {
                            log(Text.Format("SkippedFolderLink", subdirectory));
                            continue;
                        }

                        directories.Push(subdirectory);
                        subdirectories.Add(subdirectory);
                    }
                    catch (IOException exception)
                    {
                        errors++;
                        log(Text.Format("CannotReadFolder", subdirectory, exception.Message));
                    }
                    catch (UnauthorizedAccessException exception)
                    {
                        errors++;
                        log(Text.Format("NoPermissionReadingFolder", subdirectory, exception.Message));
                    }
                    catch (SecurityException exception)
                    {
                        errors++;
                        log(Text.Format("AccessDeniedReadingFolder", subdirectory, exception.Message));
                    }
                }
            }
            catch (IOException exception)
            {
                errors++;
                log(Text.Format("CannotEnumerateSubfolders", current, exception.Message));
            }
            catch (UnauthorizedAccessException exception)
            {
                errors++;
                log(Text.Format("NoPermissionEnumeratingSubfolders", current, exception.Message));
            }
            catch (SecurityException exception)
            {
                errors++;
                log(Text.Format("AccessDeniedEnumeratingSubfolders", current, exception.Message));
            }
        }

        return new FileTreeScanResult(files, subdirectories, errors);
    }

    public static bool IsSameOrDescendant(string path, string parent)
    {
        var relativePath = Path.GetRelativePath(parent, path);
        return relativePath == "." ||
               (!Path.IsPathRooted(relativePath) &&
                relativePath != ".." &&
                !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal));
    }
}
