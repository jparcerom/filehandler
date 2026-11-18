namespace FileHandler.Models;

public sealed record WindowsDuplicateSummary(
    int CandidateCount,
    int Moved,
    int CollisionRenames,
    int UnverifiedCount,
    int ChangedSinceScan,
    int Errors,
    int ScanErrors,
    int VerificationErrors,
    int FallbackOriginalCount);
