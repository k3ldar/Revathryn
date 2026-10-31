using System.Text;
using System.Text.RegularExpressions;

using Revathryn.Contracts;

namespace Revathryn.Engine
{
    /// <summary>Evaluates .gitignore rules for a local repository.</summary>
    internal sealed class GitIgnoreMatcher
    {
        private readonly string _rootPath;
        private readonly Dictionary<string, List<GitIgnoreRule>> _cache = new(StringComparer.Ordinal);

        /// <summary>Initialises a new matcher over the given repository root.</summary>
        internal GitIgnoreMatcher(string rootPath)
        {
            _rootPath = rootPath;
        }

        /// <summary>Returns true when the given repository-relative path is ignored.</summary>
        internal bool IsIgnored(string relativePath, bool isDirectory)
        {
            string directory = GetDirectory(relativePath);
            List<GitIgnoreRule> rules = GetRules(directory);

            bool ignored = false;

            foreach (GitIgnoreRule rule in rules)
            {
                if (rule.Matches(relativePath, isDirectory))
                {
                    ignored = !rule.Negated;
                }
            }

            return ignored;
        }

        private List<GitIgnoreRule> GetRules(string directory)
        {
            if (_cache.TryGetValue(directory, out List<GitIgnoreRule> cached))
            {
                return cached;
            }

            List<GitIgnoreRule> rules = [];
            string[] segments = directory.Length == 0 ? [] : directory.Split(Consts.CharForwardSlash);

            StringBuilder current = new();
            LoadRules(current.ToString(), rules);

            foreach (string segment in segments)
            {
                if (current.Length > 0)
                {
                    current.Append(Consts.CharForwardSlash);
                }

                current.Append(segment);
                LoadRules(current.ToString(), rules);
            }

            _cache[directory] = rules;
            return rules;
        }

        private void LoadRules(string baseDirectory, List<GitIgnoreRule> rules)
        {
            string directoryPath = ResolveDirectory(baseDirectory);
            string ignorePath = Path.Combine(directoryPath, Consts.GitIgnoreFileName);

            if (!File.Exists(ignorePath))
            {
                return;
            }

            foreach (string line in File.ReadLines(ignorePath))
            {
                GitIgnoreRule rule = GitIgnoreRule.Parse(line, baseDirectory);

                if (rule is not null)
                {
                    rules.Add(rule);
                }
            }
        }

        private string ResolveDirectory(string baseDirectory)
        {
            if (baseDirectory.Length == 0)
            {
                return _rootPath;
            }

            string osDirectory = baseDirectory.Replace(Consts.CharForwardSlash, Path.DirectorySeparatorChar);
            return Path.Combine(_rootPath, osDirectory);
        }

        private static string GetDirectory(string relativePath)
        {
            int separator = relativePath.LastIndexOf(Consts.CharForwardSlash);

            if (separator < 0)
            {
                return string.Empty;
            }

            return relativePath[..separator];
        }

        private sealed class GitIgnoreRule
        {
            private const string RegexPathSeparator = "/";
            private const string RegexStartAnchor = "^";
            private const string RegexEndAnchor = "$";
            private const string RegexAnyPathPrefix = "(?:.*/)?";
            private const string RegexZeroOrMore = ".*";
            private const string RegexZeroOrMoreNonSeparator = "[^/]*";
            private const string RegexSingleNonSeparator = "[^/]";
            private const int DoubleAsteriskPrefixLength = 2;

            private readonly bool _directoryOnly;
            private readonly bool _negated;
            private readonly Regex _regex;

            private GitIgnoreRule(string baseDirectory, string pattern, bool directoryOnly, bool negated)
            {
                _directoryOnly = directoryOnly;
                _negated = negated;
                _regex = new Regex(BuildRegex(baseDirectory, pattern), RegexOptions.Compiled);
            }

            public bool Negated => _negated;

            public static GitIgnoreRule Parse(string line, string baseDirectory)
            {
                string pattern = line.TrimEnd();

                if (pattern.Length == 0 || pattern.StartsWith(Consts.CharHash))
                {
                    return null;
                }

                bool negated = pattern.StartsWith(Consts.CharBang);

                if (negated)
                {
                    pattern = pattern[1..];
                }

                bool directoryOnly = pattern.EndsWith(Consts.CharForwardSlash);

                if (directoryOnly)
                {
                    pattern = pattern.TrimEnd(Consts.CharForwardSlash);
                }

                if (pattern.Length == 0)
                {
                    return null;
                }

                return new GitIgnoreRule(baseDirectory, pattern, directoryOnly, negated);
            }

            public bool Matches(string relativePath, bool isDirectory)
            {
                if (_directoryOnly && !isDirectory)
                {
                    return false;
                }

                return _regex.IsMatch(relativePath);
            }

            private static string BuildRegex(string baseDirectory, string pattern)
            {
                string prefix = baseDirectory.Length > 0 ? Regex.Escape(baseDirectory) + RegexPathSeparator : string.Empty;

                if (pattern.StartsWith(Consts.CharForwardSlash))
                {
                    return RegexStartAnchor + prefix + ConvertGlob(pattern[1..]) + RegexEndAnchor;
                }

                if (pattern.Contains(Consts.CharForwardSlash))
                {
                    return RegexStartAnchor + prefix + ConvertGlob(pattern) + RegexEndAnchor;
                }

                return RegexStartAnchor + RegexAnyPathPrefix + ConvertGlob(pattern) + RegexEndAnchor;
            }

            private static string ConvertGlob(string glob)
            {
                StringBuilder builder = new();

                for (int index = 0; index < glob.Length; index++)
                {
                    char current = glob[index];

                    if (current == Consts.CharAsterisk)
                    {
                        if (index + 1 < glob.Length && glob[index + 1] == Consts.CharAsterisk)
                        {
                            if (index + DoubleAsteriskPrefixLength < glob.Length && glob[index + DoubleAsteriskPrefixLength] == Consts.CharForwardSlash)
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

                return builder.ToString();
            }
        }
    }
}
