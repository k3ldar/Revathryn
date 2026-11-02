namespace Revathryn.Contracts.Enums
{
    /// <summary>Describes where the source context supplied to the review engine originated.</summary>
    public enum IssueValidationContextMode
    {
        /// <summary>No source context could be supplied.</summary>
        Unavailable = 0,

        /// <summary>The original snapshot captured when the finding was detected was reused.</summary>
        OriginalSnapshot = 1,

        /// <summary>
        /// No original snapshot exists, so the current repository source was supplied.
        /// This is explicitly not the context the detection engine received.
        /// </summary>
        CurrentSource = 2
    }
}
