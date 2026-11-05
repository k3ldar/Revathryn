namespace Revathryn.Contracts
{
    /// <summary>Formats and parses the comma-separated issue page-size option list.</summary>
    public static class IssuePageSizeOptionsFormatter
    {
        /// <summary>Formats page sizes as a comma-separated string.</summary>
        public static string Format(IEnumerable<int> pageSizes)
        {
            if (pageSizes is null)
            {
                return string.Empty;
            }

            return string.Join(Consts.IssuePageSizeOptionsSeparator, pageSizes);
        }

        /// <summary>Parses a comma-separated list of positive page sizes, returning an empty list when invalid.</summary>
        public static List<int> Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return [];
            }

            List<int> pageSizes = [];

            foreach (string part in value.Split(Consts.IssuePageSizeOptionsSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (!int.TryParse(part, out int pageSize) || pageSize < Consts.MinimumIssuePageSize)
                {
                    return [];
                }

                pageSizes.Add(pageSize);
            }

            return [.. pageSizes.Distinct().OrderBy(pageSize => pageSize)];
        }
    }
}
