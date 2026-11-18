using System.IO;

namespace FileHandler.Services;

public static class UniqueDestinationPath
{
    public static string GetAvailablePath(string directory, string fileName)
    {
        var desiredPath = Path.Combine(directory, fileName);
        if (!File.Exists(desiredPath) && !Directory.Exists(desiredPath))
        {
            return desiredPath;
        }

        var name = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);
        for (long suffix = 1; suffix < long.MaxValue; suffix++)
        {
            var candidate = Path.Combine(directory, $"{name}-{suffix}{extension}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException($"No se pudo generar un nombre disponible para {fileName}.");
    }
}
