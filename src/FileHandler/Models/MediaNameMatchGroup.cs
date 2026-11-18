namespace FileHandler.Models;

public sealed record MediaNameMatchGroup(
    string FileName,
    string Reason,
    IReadOnlyList<MediaDuplicateFile> Files);
