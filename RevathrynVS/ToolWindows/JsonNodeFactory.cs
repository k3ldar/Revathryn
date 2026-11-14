using System.Globalization;

using Newtonsoft.Json.Linq;

namespace RevathrynVS2026.ToolWindows
{
    /// <summary>Converts arbitrary JSON into a tree of display nodes.</summary>
    internal static class JsonNodeFactory
    {
        #region Fields
        private const string RootName = "result";

        private const string NullText = "null";
        #endregion Fields

        #region Public Methods
        /// <summary>Creates the top-level nodes for <paramref name="root"/>.</summary>
        /// <param name="root">The parsed JSON, or <see langword="null"/>.</param>
        /// <returns>The nodes to display; empty when <paramref name="root"/> is <see langword="null"/>.</returns>
        public static IReadOnlyList<JsonNodeViewModel> Create(JToken root)
        {
            if (root == null)
            {
                return [];
            }

            if (root is JContainer)
            {
                return CreateChildren(root);
            }

            return [CreateNode(RootName, root)];
        }

        #endregion Public Methods

        #region Private Methods
        private static JsonNodeViewModel CreateNode(string name, JToken token)
        {
            if (token is JContainer)
            {
                return new JsonNodeViewModel(name, null, CreateChildren(token));
            }

            return new JsonNodeViewModel(name, FormatValue(token), []);
        }

        private static IReadOnlyList<JsonNodeViewModel> CreateChildren(JToken container)
        {
            List<JsonNodeViewModel> children = [];

            if (container is JObject obj)
            {
                foreach (JProperty property in obj.Properties())
                {
                    children.Add(CreateNode(property.Name, property.Value));
                }
            }
            else if (container is JArray array)
            {
                for (int index = 0; index < array.Count; index++)
                {
                    children.Add(CreateNode($"[{index.ToString(CultureInfo.InvariantCulture)}]", array[index]));
                }
            }

            return children;
        }

        private static string FormatValue(JToken token)
        {
            if (token.Type == JTokenType.Null || token.Type == JTokenType.Undefined)
            {
                return NullText;
            }

            return Convert.ToString(((JValue)token).Value, CultureInfo.InvariantCulture);
        }

        #endregion Private Methods
    }
}
