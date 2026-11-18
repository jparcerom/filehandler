using System.IO;
using System.Security;
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
        var errors = 0;
        var subdirectoryCount = 0;
        var directories = new Stack<string>();
        directories.Push(sourcePath);

        while (directories.Count > 0)
        {
            var current = directories.Pop();
            progress?.Report(new OperationProgress(
                files.Count,
                0,
                $"Escaneando {current} · {files.Count} archivos encontrados.",
                IsScanning: true));

            try
            {
                files.AddRange(Directory.EnumerateFiles(current));
            }
            catch (IOException exception)
            {
                errors++;
                log($"[ERROR] No se pudo recorrer {current}: {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                errors++;
                log($"[ERROR] Sin permiso para recorrer {current}: {exception.Message}");
            }
            catch (SecurityException exception)
            {
                errors++;
                log($"[ERROR] Acceso denegado al recorrer {current}: {exception.Message}");
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
                            log($"[OMITIDO] No se recorrió el enlace de carpeta: {subdirectory}");
                            continue;
                        }

                        directories.Push(subdirectory);
                        subdirectoryCount++;
                    }
                    catch (IOException exception)
                    {
                        errors++;
                        log($"[ERROR] No se pudo leer la carpeta {subdirectory}: {exception.Message}");
                    }
                    catch (UnauthorizedAccessException exception)
                    {
                        errors++;
                        log($"[ERROR] Sin permiso para leer la carpeta {subdirectory}: {exception.Message}");
                    }
                    catch (SecurityException exception)
                    {
                        errors++;
                        log($"[ERROR] Acceso denegado al leer la carpeta {subdirectory}: {exception.Message}");
                    }
                }
            }
            catch (IOException exception)
            {
                errors++;
                log($"[ERROR] No se pudieron enumerar las subcarpetas de {current}: {exception.Message}");
            }
            catch (UnauthorizedAccessException exception)
            {
                errors++;
                log($"[ERROR] Sin permiso para enumerar las subcarpetas de {current}: {exception.Message}");
            }
            catch (SecurityException exception)
            {
                errors++;
                log($"[ERROR] Acceso denegado al enumerar las subcarpetas de {current}: {exception.Message}");
            }
        }

        return new FileTreeScanResult(files, subdirectoryCount, errors);
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
