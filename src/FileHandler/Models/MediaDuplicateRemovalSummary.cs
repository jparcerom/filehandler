namespace FileHandler.Models;

public sealed record MediaDuplicateRemovalSummary(
    IReadOnlyList<string> SentToRecycleBin,
    int ChangedSinceScan,
    int Errors);
