namespace FileHandler.Models;

public sealed record WindowsDuplicateScanResult(
    string SourcePath,
    IReadOnlyList<VerifiedWindowsDuplicate> VerifiedDuplicates,
    int CandidateCount,
    int UnverifiedCount,
    int FallbackOriginalCount,
    int ScanErrors,
    int VerificationErrors);
