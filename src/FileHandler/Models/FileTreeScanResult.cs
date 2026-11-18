namespace FileHandler.Models;

public sealed record FileTreeScanResult(
    IReadOnlyList<string> Files,
    int SubdirectoryCount,
    int Errors);
