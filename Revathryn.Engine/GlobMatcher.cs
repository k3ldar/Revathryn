using System.Text;
using System.Text.RegularExpressions;

using Revathryn.Contracts;

namespace Revathryn.Engine
{
    /// <summary>Matches repository-relative paths against simple glob patterns.</summary>
    internal static class GlobMatcher
    {
        private const string RegexStartAnchor = "^";
        private const string RegexEndAnchor = "$";
        private const string RegexAnyPathPrefix = "(?:.*/)?";
        private const string RegexZeroOrMore = ".*";
        private const string RegexZeroOrMoreNonSeparator = "[^/]*";
        private const string RegexSingleNonSeparator = "[^/]";
        private const int DoubleAsteriskPrefixLength = 2;

        /// <summary>Returns true when the path matches the pattern.</summary>
        internal static bool IsMatch(string pattern, string relativePath)
        {
            Regex regex = new(ConvertToRegex(pattern), RegexOptions.Compiled | RegexOptions.IgnoreCase);
            return regex.IsMatch(relativePath);
        }

        private static string ConvertToRegex(string pattern)
        {
            string normalised = pattern.Replace(Consts.CharBackSlash, Consts.CharForwardSlash);
            StringBuilder builder = new();

            for (int index = 0; index < normalised.Length; index++)
            {
                char current = normalised[index];

                if (current == Consts.CharAsterisk)
                {
                    if (index + 1 < normalised.Length && normalised[index + 1] == Consts.CharAsterisk)
                    {
                        if (index + DoubleAsteriskPrefixLength < normalised.Length && normalised[index + DoubleAsteriskPrefixLength] == Consts.CharForwardSlash)
                        {
                            builder.Append(RegexAnyPathPrefix);
                            index += DoubleAsteriskPrefixLength;
                        }
                        else
                        {
                            builder.Append(RegexZeroOrMore);
                            index++;
                        }
                    }
                    else
                    {
                        builder.Append(RegexZeroOrMoreNonSeparator);
                    }

                    continue;
                }

                if (current == Consts.CharQuestionMark)
                {
                    builder.Append(RegexSingleNonSeparator);
                    continue;
                }

                builder.Append(Regex.Escape(current.ToString()));
            }

            return RegexStartAnchor + builder + RegexEndAnchor;
        }
    }
}
