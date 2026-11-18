using System.IO;
using System.Text;

namespace FileHandler.Services;

public sealed class ActivityLog : IDisposable
{
    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FileHandler",
        "logs",
        "filehandler.log");

    private readonly StreamWriter _writer;

    public ActivityLog(string? path = null)
    {
        LogPath = path ?? DefaultPath;
        var directory = Path.GetDirectoryName(LogPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(LogPath, append: true, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true
        };
    }

    public string LogPath { get; }

    public void Write(string message)
    {
        _writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
    }

    public void Dispose()
    {
        _writer.Dispose();
    }
}
