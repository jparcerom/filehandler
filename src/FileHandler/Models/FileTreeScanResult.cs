namespace FileHandler.Models;

public sealed record FileTreeScanResult(
    IReadOnlyList<string> Files,
    IReadOnlyList<string> Directories,
    int Errors)
{
    public int SubdirectoryCount => Directories.Count;
}
