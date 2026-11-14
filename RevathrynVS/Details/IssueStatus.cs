namespace RevathrynVS2026.Details
{
    /// <summary>Review status of a finding, using the numeric values returned by the endpoint.</summary>
    internal enum IssueStatus
    {
        New = 0,
        Reviewed = 1,
        FalsePositive = 2,
        Resolved = 3,
        Deferred = 4
    }
}
