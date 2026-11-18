namespace FileHandler.Models;

public sealed record MediaDuplicateGroup(
    string Sha256,
    long Size,
    IReadOnlyList<MediaDuplicateFile> Files);
