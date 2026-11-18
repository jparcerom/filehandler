namespace FileHandler.Models;

public sealed record MediaScanResult(
    string SourcePath,
    IReadOnlyList<string> ImageFiles,
    IReadOnlyList<string> VideoFiles,
    IReadOnlyList<string> Directories,
    int Errors)
{
    public int SubdirectoryCount => Directories.Count;
}
