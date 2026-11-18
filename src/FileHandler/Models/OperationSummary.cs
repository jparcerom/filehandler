namespace FileHandler.Models;

public sealed record OperationSummary(int Total, int Moved, int Skipped, int Errors);
