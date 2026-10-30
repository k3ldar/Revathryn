namespace Revathryn.Contracts.Enums
{
    /// <summary>Category of a finding, distinguishing code issues from operational failures.</summary>
    public enum FindingCategory
    {
        /// <summary>A defect or improvement identified in source code.</summary>
        CodeIssue = 0,

        /// <summary>An operational failure that requires investigation.</summary>
        OperationalFailure = 1,

        /// <summary>General information without a defect classification.</summary>
        Information = 2
    }
}
