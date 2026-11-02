using System.Diagnostics.CodeAnalysis;

using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Review;

namespace Revathryn.Review.Tests
{
    [TestClass]
    [ExcludeFromCodeCoverage]
    public class IssueValidationResponseParserTests
    {
        private static readonly Dictionary<string, int> Sources = new() { ["src/Example.cs"] = 100 };

        [TestMethod]
        [DataRow("Agreed", IssueValidationVerdict.Agreed)]
        [DataRow("Disagreed", IssueValidationVerdict.Disagreed)]
        [DataRow("PartiallyAgreed", IssueValidationVerdict.PartiallyAgreed)]
        [DataRow("Inconclusive", IssueValidationVerdict.Inconclusive)]
        public void Parse_HandlesEachSupportedVerdict(string verdictText, IssueValidationVerdict expected)
        {
            string json = $"{{\"verdict\":\"{verdictText}\",\"confidence\":0.5,\"summary\":\"S\",\"reasoning\":\"R\"}}";

            IssueValidationEngineResult result = IssueValidationResponseParser.Parse(json, Sources);

            Assert.IsTrue(result.Success);
            Assert.IsFalse(result.Malformed);
            Assert.AreEqual(expected, result.Verdict);
        }

        [TestMethod]
        [DataRow("partially agreed")]
        [DataRow("PARTIALLY_AGREED")]
        [DataRow("partially-agreed")]
        public void Parse_AcceptsVerdictSpellingVariations(string verdictText)
        {
            string json = $"{{\"verdict\":\"{verdictText}\",\"summary\":\"S\"}}";

            IssueValidationEngineResult result = IssueValidationResponseParser.Parse(json, Sources);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(IssueValidationVerdict.PartiallyAgreed, result.Verdict);
        }

        [TestMethod]
        public void Parse_ReadsFullStructuredResponse()
        {
            const string json = """
                {
                  "verdict": "PartiallyAgreed",
                  "confidence": 0.91,
                  "summary": "Valid but overstated.",
                  "reasoning": "Narrower path.",
                  "severity": { "original": "High", "recommended": "Medium", "reasoning": "Limited path." },
                  "evidence": [ { "file": "src/Example.cs", "lineStart": 42, "lineEnd": 48, "explanation": "No null check." } ],
                  "suggestions": [ "Narrow the description." ],
                  "limitations": [ "Callers not supplied." ]
                }
                """;

            IssueValidationEngineResult result = IssueValidationResponseParser.Parse(json, Sources);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(0.91, result.Confidence);
            Assert.AreEqual("Valid but overstated.", result.Summary);
            Assert.AreEqual("Narrower path.", result.Reasoning);
            Assert.AreEqual(FindingSeverity.Medium, result.RecommendedSeverity);
            Assert.AreEqual("Limited path.", result.SeverityReasoning);
            Assert.HasCount(1, result.Evidence);
            Assert.AreEqual("src/Example.cs", result.Evidence[0].File);
            Assert.AreEqual(42, result.Evidence[0].LineStart);
            Assert.AreEqual(48, result.Evidence[0].LineEnd);
            Assert.HasCount(1, result.Suggestions);
            Assert.HasCount(1, result.Limitations);
        }

        [TestMethod]
        public void Parse_StripsMarkdownCodeFence()
        {
            const string json = "```json\n{\"verdict\":\"Agreed\",\"summary\":\"Fine\"}\n```";

            IssueValidationEngineResult result = IssueValidationResponseParser.Parse(json, Sources);

            Assert.IsTrue(result.Success);
            Assert.AreEqual(IssueValidationVerdict.Agreed, result.Verdict);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow("not json at all")]
        [DataRow("[1,2,3]")]
        [DataRow("{\"verdict\":\"Maybe\",\"summary\":\"S\"}")]
        [DataRow("{\"summary\":\"S\"}")]
        [DataRow("{\"verdict\":\"Agreed\"}")]
        [DataRow("{\"verdict\":42,\"summary\":\"S\"}")]
        [DataRow("{\"verdict\":\"Agreed\",\"summary\":\"S\"")]
        public void Parse_TreatsInvalidOutputAsMalformedAndNeverAsAVerdict(string content)
        {
            IssueValidationEngineResult result = IssueValidationResponseParser.Parse(content, Sources);

            Assert.IsFalse(result.Success);
            Assert.IsTrue(result.Malformed);
            Assert.IsFalse(string.IsNullOrWhiteSpace(result.ErrorMessage));
            Assert.AreEqual(content ?? string.Empty, result.RawResponse);
        }

        [TestMethod]
        public void Parse_DoesNotManufactureConfidenceWhenAbsent()
        {
            IssueValidationEngineResult result = IssueValidationResponseParser.Parse("{\"verdict\":\"Agreed\",\"summary\":\"S\"}", Sources);

            Assert.IsNull(result.Confidence);
        }

        [TestMethod]
        [DataRow("0", 0.0)]
        [DataRow("1", 1.0)]
        [DataRow("0.25", 0.25)]
        [DataRow("91", 0.91)]
        [DataRow("\"91%\"", 0.91)]
        [DataRow("\"0.4\"", 0.4)]
        public void Parse_NormalisesConfidence(string confidenceJson, double expected)
        {
            string json = $"{{\"verdict\":\"Agreed\",\"confidence\":{confidenceJson},\"summary\":\"S\"}}";

            IssueValidationEngineResult result = IssueValidationResponseParser.Parse(json, Sources);

            Assert.IsNotNull(result.Confidence);
            Assert.AreEqual(expected, result.Confidence.Value, 0.0001);
        }

        [TestMethod]
        [DataRow("-0.5")]
        [DataRow("150")]
        [DataRow("\"high\"")]
        [DataRow("null")]
        public void Parse_DiscardsUnusableConfidence(string confidenceJson)
        {
            string json = $"{{\"verdict\":\"Agreed\",\"confidence\":{confidenceJson},\"summary\":\"S\"}}";

            IssueValidationEngineResult result = IssueValidationResponseParser.Parse(json, Sources);

            Assert.IsTrue(result.Success);
            Assert.IsNull(result.Confidence);
        }

        [TestMethod]
        public void NormaliseConfidence_HandlesBoundaryAndInvalidValues()
        {
            Assert.IsNull(IssueValidationResponseParser.NormaliseConfidence(null));
            Assert.IsNull(IssueValidationResponseParser.NormaliseConfidence(double.NaN));
            Assert.IsNull(IssueValidationResponseParser.NormaliseConfidence(double.PositiveInfinity));
            Assert.IsNull(IssueValidationResponseParser.NormaliseConfidence(-1));
            Assert.IsNull(IssueValidationResponseParser.NormaliseConfidence(100.5));
            Assert.AreEqual(1.0, IssueValidationResponseParser.NormaliseConfidence(100).Value, 0.0001);
            Assert.AreEqual(0.0, IssueValidationResponseParser.NormaliseConfidence(0).Value, 0.0001);
        }

        [TestMethod]
        public void Parse_DiscardsEvidenceNotBackedBySuppliedContext()
        {
            const string json = """
                {
                  "verdict": "Agreed",
                  "summary": "S",
                  "evidence": [
                    { "file": "src/Example.cs", "lineStart": 1, "lineEnd": 5, "explanation": "real" },
                    { "file": "src/Invented.cs", "lineStart": 1, "lineEnd": 2, "explanation": "invented file" },
                    { "file": "src/Example.cs", "lineStart": 90, "lineEnd": 500, "explanation": "beyond the file" },
                    { "file": "src/Example.cs", "lineStart": 0, "lineEnd": 3, "explanation": "line zero" }
                  ]
                }
                """;

            IssueValidationEngineResult result = IssueValidationResponseParser.Parse(json, Sources);

            Assert.IsTrue(result.Success);
            Assert.HasCount(1, result.Evidence);
            Assert.AreEqual("real", result.Evidence[0].Explanation);
            Assert.HasCount(1, result.Limitations);
            Assert.Contains("3", result.Limitations[0]);
        }

        [TestMethod]
        public void Parse_MatchesEvidencePathsIgnoringSlashStyleAndCase()
        {
            const string json = "{\"verdict\":\"Agreed\",\"summary\":\"S\",\"evidence\":[{\"file\":\".\\\\SRC\\\\example.cs\",\"lineStart\":3,\"lineEnd\":4,\"explanation\":\"x\"}]}";

            IssueValidationEngineResult result = IssueValidationResponseParser.Parse(json, Sources);

            Assert.HasCount(1, result.Evidence);
            Assert.AreEqual("SRC/example.cs", result.Evidence[0].File);
        }

        [TestMethod]
        public void Parse_ClampsReversedLineRangeToSingleLine()
        {
            const string json = "{\"verdict\":\"Agreed\",\"summary\":\"S\",\"evidence\":[{\"file\":\"src/Example.cs\",\"lineStart\":10,\"lineEnd\":4,\"explanation\":\"x\"}]}";

            IssueValidationEngineResult result = IssueValidationResponseParser.Parse(json, Sources);

            Assert.HasCount(1, result.Evidence);
            Assert.AreEqual(10, result.Evidence[0].LineStart);
            Assert.AreEqual(10, result.Evidence[0].LineEnd);
        }

        [TestMethod]
        public void Parse_DiscardsAllEvidenceWhenNoSourcesWereSupplied()
        {
            const string json = "{\"verdict\":\"Agreed\",\"summary\":\"S\",\"evidence\":[{\"file\":\"src/Example.cs\",\"lineStart\":1,\"lineEnd\":2,\"explanation\":\"x\"}]}";

            IssueValidationEngineResult result = IssueValidationResponseParser.Parse(json, null);

            Assert.IsTrue(result.Success);
            Assert.IsEmpty(result.Evidence);
            Assert.HasCount(1, result.Limitations);
        }

        [TestMethod]
        public void Parse_IgnoresUnknownSeverityAndKeepsVerdict()
        {
            const string json = "{\"verdict\":\"Agreed\",\"summary\":\"S\",\"severity\":{\"recommended\":\"Catastrophic\",\"reasoning\":\"why\"}}";

            IssueValidationEngineResult result = IssueValidationResponseParser.Parse(json, Sources);

            Assert.IsTrue(result.Success);
            Assert.IsNull(result.RecommendedSeverity);
            Assert.AreEqual("why", result.SeverityReasoning);
        }

        [TestMethod]
        public void Parse_AcceptsRecommendedSeverityCaseInsensitively()
        {
            const string json = "{\"verdict\":\"Agreed\",\"summary\":\"S\",\"severity\":{\"recommended\":\"critical\"}}";

            IssueValidationEngineResult result = IssueValidationResponseParser.Parse(json, Sources);

            Assert.AreEqual(FindingSeverity.Critical, result.RecommendedSeverity);
        }

        [TestMethod]
        public void Parse_IgnoresNonStringSuggestions()
        {
            const string json = "{\"verdict\":\"Agreed\",\"summary\":\"S\",\"suggestions\":[\"keep\",1,null,\"  \"],\"limitations\":\"not an array\"}";

            IssueValidationEngineResult result = IssueValidationResponseParser.Parse(json, Sources);

            Assert.HasCount(1, result.Suggestions);
            Assert.AreEqual("keep", result.Suggestions[0]);
            Assert.IsEmpty(result.Limitations);
        }

        [TestMethod]
        public void TryParseVerdict_RejectsEmptyAndUnknownValues()
        {
            Assert.IsFalse(IssueValidationResponseParser.TryParseVerdict(null, out _));
            Assert.IsFalse(IssueValidationResponseParser.TryParseVerdict(" ", out _));
            Assert.IsFalse(IssueValidationResponseParser.TryParseVerdict("Failed", out _));
            Assert.IsTrue(IssueValidationResponseParser.TryParseVerdict("inconclusive", out IssueValidationVerdict verdict));
            Assert.AreEqual(IssueValidationVerdict.Inconclusive, verdict);
        }
    }
}
