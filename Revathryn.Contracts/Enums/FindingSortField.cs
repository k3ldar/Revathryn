namespace Revathryn.Contracts.Enums
{
    /// <summary>Fields by which findings can be ordered.</summary>
    public enum FindingSortField
    {
        /// <summary>Order by creation date.</summary>
        CreatedDate = 0,

        /// <summary>Order by last modified date.</summary>
        LastModifiedDate = 1,

        /// <summary>Order by severity.</summary>
        Severity = 2,

        /// <summary>Order by title.</summary>
        Title = 3
    }
}
