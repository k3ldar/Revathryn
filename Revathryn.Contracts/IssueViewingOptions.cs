namespace Revathryn.Contracts
{
    /// <summary>Configuration for the issue browsing experience.</summary>
    public sealed class IssueViewingOptions
    {
        /// <summary>Default page size used when none is supplied by the user.</summary>
        public int DefaultPageSize { get; set; } = Consts.IssueDefaultPageSize;

        /// <summary>Page sizes offered to the user.</summary>
        public int[] PageSizeOptions { get; set; } =
        [
            Consts.IssuePageSizeSmall,
            Consts.IssuePageSizeMedium,
            Consts.IssuePageSizeLarge,
            Consts.IssuePageSizeXLarge
        ];
    }
}
