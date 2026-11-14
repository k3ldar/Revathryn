using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace RevathrynVS2026.ToolWindows
{
    /// <summary>Text block that renders the small Markdown subset used by the architecture summary.</summary>
    /// <remarks>Supports headings, bullet lists, **bold** and `code`.</remarks>
    internal sealed class MarkdownTextBlock : TextBlock
    {
        #region Fields
        public static readonly DependencyProperty MarkdownProperty = DependencyProperty.Register(
            nameof(Markdown),
            typeof(string),
            typeof(MarkdownTextBlock),
            new PropertyMetadata(null, OnMarkdownChanged));

        private static readonly Regex InlinePattern = new(@"\*\*(?<bold>.+?)\*\*|`(?<code>[^`]+)`", RegexOptions.Compiled);

        private static readonly Regex BulletPattern = new(@"^(?<indent>\s*)[-*]\s+(?<text>.*)$", RegexOptions.Compiled);

        private static readonly Regex HeadingPattern = new(@"^#{1,6}\s+(?<text>.*)$", RegexOptions.Compiled);

        private static readonly Regex RulePattern = new(@"^\s*(-{3,}|\*{3,})\s*$", RegexOptions.Compiled);

        private static readonly FontFamily CodeFont = new("Consolas");

        private const string BulletPrefix = "\u2022  ";

        private const int IndentSpacesPerLevel = 2;
        #endregion Fields

        #region Properties
        /// <summary>Gets or sets the Markdown to render.</summary>
        public string Markdown
        {
            get => (string)GetValue(MarkdownProperty);
            set => SetValue(MarkdownProperty, value);
        }
        #endregion Properties

        #region Private Methods
        private static void OnMarkdownChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            ((MarkdownTextBlock)sender).Render((string)e.NewValue);
        }

        private void Render(string markdown)
        {
            Inlines.Clear();

            if (string.IsNullOrEmpty(markdown))
            {
                return;
            }

            bool previousBlank = true;
            bool first = true;

            foreach (string rawLine in markdown.Replace("\r\n", "\n").Split('\n'))
            {
                string line = rawLine.TrimEnd();

                if (RulePattern.IsMatch(line))
                {
                    continue;
                }

                if (line.Length == 0)
                {
                    previousBlank = true;
                    continue;
                }

                if (!first)
                {
                    Inlines.Add(new LineBreak());

                    if (previousBlank)
                    {
                        Inlines.Add(new LineBreak());
                    }
                }

                first = false;
                previousBlank = false;
                RenderLine(line);
            }
        }

        private void RenderLine(string line)
        {
            Match heading = HeadingPattern.Match(line);

            if (heading.Success)
            {
                Inlines.Add(new Bold(new Run(heading.Groups["text"].Value)));
                return;
            }

            Match bullet = BulletPattern.Match(line);

            if (bullet.Success)
            {
                int level = bullet.Groups["indent"].Length / IndentSpacesPerLevel;
                Inlines.Add(new Run(new string(' ', level * 4) + BulletPrefix));
                AddInline(bullet.Groups["text"].Value);
                return;
            }

            AddInline(line);
        }

        private void AddInline(string text)
        {
            int position = 0;

            foreach (Match match in InlinePattern.Matches(text))
            {
                if (match.Index > position)
                {
                    Inlines.Add(new Run(text.Substring(position, match.Index - position)));
                }

                if (match.Groups["bold"].Success)
                {
                    Inlines.Add(new Bold(new Run(match.Groups["bold"].Value)));
                }
                else
                {
                    Inlines.Add(new Run(match.Groups["code"].Value) { FontFamily = CodeFont });
                }

                position = match.Index + match.Length;
            }

            if (position < text.Length)
            {
                Inlines.Add(new Run(text.Substring(position)));
            }
        }

        #endregion Private Methods
    }
}
