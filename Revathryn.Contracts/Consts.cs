namespace Revathryn.Contracts
{
    public static class Consts
    {
        public const string DefaultProgramDataPath = "Revathryn";
        public const string DefaultBackupDirectoryName = "backups";
        public const string DefaultDatabaseFileName = "revathryn.db";
        public const string DatabasePath = "db";
        public const string DefaultDataStore = "Data Source={0}";
        public const string SqlitePoolingDisabled = "Pooling=False";
        public const string FileTypeCSharp = "cs";
        public const string CEpExtensions = "c,cpp,h,hpp,cc,cxx,hxx";
        public const string FileTypeJson = "json";
        public const string FileTypeXml = "xml";
        public const string FileTypeConfig = "config";
        public const string FileTypeCSharpProj = "csproj";
        public const string FileTypeSolution = "sln";
        public const string FileTypeSolutionXml = "slnx";
        public const string GitIgnoreFileName = ".gitignore";
        public const int BinaryDetectionBufferSize = 8000;
        public const string FileTypeProps = "props";
        public const string FileTypeTargets = "targets";
        public const string FileTypeMarkdown = "md";
        public const string FileTypeYml = "yml";
        public const string FileTypeYaml = "yaml";
        public const string PathAnalysisOutput = "analysis-output";
        public const string AnalysisManifestPrefix = "analysis-manifest-";
        public const string SymbolIndexDatabaseFileName = "symbol-index.db";
        public const string DefaultRepositoryFolderName = "repository";
        public const string Star = "*";
        public const char CharAsterisk = '*';
        public const char CharQuestionMark = '?';
        public const char CharHash = '#';
        public const char CharBang = '!';
        public const string FilePathGit = ".git";
        public const string FilePathVSInternal = ".vs";
        public const string FilePathBin = "bin";
        public const string FilePathObj = "obj";
        public const string FilePathNodeModules = "node_modules";
        public const string FilePathPackages = "packages";
        public const string FilePathBuild = "build";
        public const string FilePathDist = "dist";
        public const string JsonFileExtension = "json";
        public const string CSharpFilePattern = Star + FullStop + FileTypeCSharp;
        public const char CharLessThan = '<';
        public const char CharGreaterThan = '>';
        public const string StringQuestionMark = "?";
        public const string Pipe = "|";
        public const char CharForwardSlash = '/';
        public const char CharBackSlash = '\\';
        public const char CharFullStop = '.';
        public const char CharI = 'I';
        public const char CharNewLine = '\n';
        public const char CharComma = ',';
        public const char CharLeftSquareBracket = '[';
        public const char CharRightSquareBracket = ']';
        public const char CharSpace = ' ';
        public const char CharPlus = '+';
        public const char CharHyphen = '-';
        public const char CharLeftParenthesis = '(';
        public const char CharRightParenthesis = ')';
        public const char CharColon = ':';
        public const char CharTilde = '~';
        public const string FullStop = ".";
        public const string StringArrayIdentifier = "[]";
        public const string StringSpace = " ";
        public const string LanguageType = "CSharp";
        public const string LanguageVersionNumber = "14.0";
        public const string ModifierPublic = "public";
        public const string ModifierPrivate = "private";
        public const string ModifierProtected = "protected";
        public const string ModifierInternal = "internal";
        public const string ModifierProtectedInternal = ModifierProtected + StringSpace + ModifierInternal;
        public const string ModifierStatic = "static";
        public const string ModifierAbstract = "abstract";
        public const string ModifierSealed = "sealed";
        public const string OperatorKeyword = "operator";
        public const string KeywordThis = "this";

        public const string TypeInt = "int";
        public const string TypeUshort = "ushort";
        public const string TypeVoid = "void";
        public const string TypeBool = "bool";
        public const string TypeByte = "byte";
        public const string TypeSByte = "sbyte";
        public const string TypeShort = "short";
        public const string TypeUint = "uint";
        public const string TypeLong = "long";
        public const string TypeULong = "ulong";
        public const string TypeFloat = "float";
        public const string TypeDouble = "double";
        public const string TypeDecimal = "decimal";
        public const string TypeChar = "char";
        public const string TypeString = "string";
        public const string TypeObject = "object";
        public const string TypeDynamic = "dynamic";
        public const string TypeNetString = "String";
        public const string TypeNetBoolean = "Boolean";
        public const string TypeNetByte = "Byte";
        public const string TypeNetSByte = "SByte";
        public const string TypeNetInt16 = "Int16";
        public const string TypeNetUInt16 = "UInt16";
        public const string TypeNetInt32 = "Int32";
        public const string TypeNetUInt32 = "UInt32";
        public const string TypeNetInt64 = "Int64";
        public const string TypeNetUInt64 = "UInt64";
        public const string TypeNetSingle = "Single";
        public const string TypeNetDouble = "Double";
        public const string TypeNetDecimal = "Decimal";
        public const string TypeNetChar = "Char";
        public const string TypeNetObject = "Object";
        public const string TypeTask = "Task";
        public const string TypeValueTask = "ValueTask";
        public const string TypeAction = "Action";
        public const string TypeFunc = "Func";
        public const string TypePredicate = "Predicate";
        public const string TypeEventHandler = "EventHandler";
        public const string TypeList = "List";
        public const string TypeDictionary = "Dictionary";
        public const string TypeHashSet = "HashSet";
        public const string TypeIEnumerable = "IEnumerable";
        public const string TypeIList = "IList";
        public const string TypeICollection = "ICollection";
        public const string TypeIReadOnlyList = "IReadOnlyList";
        public const string TypeIReadOnlyCollection = "IReadOnlyCollection";
        public const string TypeIReadOnlyDictionary = "IReadOnlyDictionary";
        public const string TypeIDictionary = "IDictionary";
        public const string TypeIEnumerator = "IEnumerator";
        public const string TypeArray = "Array";
        public const string TypeSpan = "Span";
        public const string TypeReadOnlySpan = "ReadOnlySpan";
        public const string TypeMemory = "Memory";
        public const string TypeReadOnlyMemory = "ReadOnlyMemory";
        public const string TypeNullable = "Nullable";
        public const string TypeType = "Type";
        public const string TypeException = "Exception";
        public const string TypeArgumentException = "ArgumentException";
        public const string TypeArgumentNullException = "ArgumentNullException";
        public const string TypeNotImplementedException = "NotImplementedException";
        public const string TypeInvalidOperationException = "InvalidOperationException";
        public const string TypeCancellationToken = "CancellationToken";
        public const string TypeDateTime = "DateTime";
        public const string TypeDateTimeOffset = "DateTimeOffset";
        public const string TypeTimeSpan = "TimeSpan";
        public const string TypeGuid = "Guid";
        public const string TypeUri = "Uri";
        public const string TypeEnum = "Enum";
        public const string TypeVar = "var";
        public const string TypeNameOf = "nameof";
        public const string TypeTypeOf = "typeof";
        public const string TypeSizeOf = "sizeof";
        public const string TypeDefault = "default";
        public const string TypeNull = "null";
        public const string TypeValueTrue = "true";
        public const string TypeValueFalse = "false";
        public const string AnalysisVersion = "1";
        public const int DefaultCSharpReviewPriority = 100;
        public const int DefaultGeneralReviewPriority = 50;
        public const int DefaultReviewTypePriority = 1;
        public const int MinimumRepositoryId = 1;
        public const int DefaultMaxContextRequests = 3;
        public const int MaxContextItemsPerRequest = 10;

        // Automatic re-review thresholds for repositories.
        public const int DefaultAutoReviewMinimumSeverity = (int)Revathryn.Contracts.Enums.FindingSeverity.High;
        public const int MinimumAutoReviewConfidenceThreshold = 0;
        public const int MaximumAutoReviewConfidenceThreshold = 100;
        public const int DefaultAutoReviewConfidenceThreshold = 0;
        public const double AutoReviewConfidencePercentageScale = 100.0;
        public const int MaxContextItemContentLength = 4000;
        public const string ContextKeySymbols = "symbols";
        public const string ContextKeyDependencies = "dependencies";
        public const string ContextKeyFiles = "files";
        public const string ContextKeyCallers = "callers";
        public const string ContextKeySearch = "search";
        public const string ContextKeyRelated = "related";
        public const string ContextLabelReferences = "references";
        public const string ContextLabelIsUsedBy = "is used by";
        public const string MemberKindMethod = "Method";
        public const string MemberKindProperty = "Property";
        public const string MemberKindField = "Field";
        public const string MemberKindConstructor = "Constructor";
        public const string MemberKindDestructor = "Destructor";
        public const string MemberKindIndexer = "Indexer";
        public const string MemberKindOperator = "Operator";
        public const string MemberKindEnumMember = "EnumMember";
        public const string FindingFingerprintSeparator = ":";
        public const string FindingKeyRulePrefix = "rule:";
        public const string FindingKeyLocationPrefix = "loc:";
        public const string OperationalFailureFingerprintSuffix = "operational";
        public const int OpenAIRequestTimeoutSeconds = 120;
        public const string OpenAIChatCompletionsSuffix = "/chat/completions";
        public const string OpenAIResponseFormatJsonObject = "json_object";
        public const int DefaultAIMaxTokens = 4096;
        public const int MinimumAIMaxTokens = 1;
        public const int AIReviewCorrectionMaxAttempts = 1;
        public const int ReviewWorkerDelayMilliseconds = 30000;
        public const int ReviewWorkerMaxItemsPerCycle = 100;
        public const int MaximumThreadPoolSize = 500;
        public const int MaximumConcurrentRunningThreads = 50;
        public const int ThreadCpuChangeNotificationDisabledValue = 0;

        public const int CpuDecimalPlaces = 2;
        public const double CpuUsagePercentageScale = 100.0;
        public const long BytesPerKilobyte = 1024;

        // SQLite error codes used to classify transient resource contention.
        public const int SqliteBusyErrorCode = 5;
        public const int SqliteLockedErrorCode = 6;
        public const int SqliteBusySnapshotErrorCode = 517;
        public const string SqliteExceptionTypeName = "SqliteException";
        public const string SqliteErrorCodePropertyName = "SqliteErrorCode";

        // Reliability defaults.
        public const int DefaultRetryMaxAttempts = 3;
        public const int DefaultRetryInitialDelayMilliseconds = 200;
        public const int DefaultRetryMaxDelaySeconds = 30;
        public const double DefaultRetryBackoffMultiplier = 2.0;
        public const double DefaultRetryJitterFactor = 0.25;
        public const int ShutdownTimeoutSeconds = 30;

        // Review queue reliability defaults.
        public const int DefaultQueueLeaseMinutes = 10;
        public const int DefaultQueuePriorityDecayFactor = 2;
        public const int DefaultQueueMinimumPriority = 1;
        public const int MinimumPriorityDecayFactor = 1;

        // Reliability component names.
        public const string ReviewProcessorComponent = "ReviewProcessor";
        public const string RepositoryProcessorComponent = "RepositoryProcessor";
        public const string ReviewWorkerServiceComponent = "ReviewWorkerService";
        public const string StartupRecoveryServiceComponent = "StartupRecoveryService";
        public const string HealthServiceComponent = "HealthService";
        public const string OpenAIReviewEngineComponent = "OpenAIReviewEngine";
        public const string SqliteReviewOutcomeStoreComponent = "SqliteReviewOutcomeStore";

        // Reliability operation names.
        public const string ProcessNextOperation = "ProcessNext";
        public const string ProcessItemOperation = "ProcessItem";
        public const string LoadRepositoryOperation = "LoadRepository";
        public const string LoadFileOperation = "LoadFile";
        public const string LoadReviewTypeOperation = "LoadReviewType";
        public const string ReadFileOperation = "ReadFile";
        public const string ReviewOperation = "Review";
        public const string PersistOutcomeOperation = "PersistOutcome";
        public const string RecordOperationalFailureOperation = "RecordOperationalFailure";
        public const string ProcessRepositoryOperation = "ProcessRepository";
        public const string AnalyseCSharpFilesOperation = "AnalyseCSharpFiles";
        public const string RecoverOperation = "Recover";
        public const string RunCycleOperation = "RunCycle";
        public const string CheckReadinessOperation = "CheckReadiness";
        public const string CheckLivenessOperation = "CheckLiveness";

        // Health check names.
        public const string DatabaseConnectivityCheck = "DatabaseConnectivity";
        public const string BackgroundProcessingCheck = "BackgroundProcessing";
        public const string AnalyserAvailabilityCheck = "AnalyserAvailability";
        public const string RepositoryAnalyserCheck = "RepositoryAnalyser";
        public const string LivenessCheck = "Liveness";
        public const string ReviewProcessingCheck = "ReviewProcessing";
        public const string ProcessingPauseCheck = "ProcessingPause";

        // Issue query defaults.
        public const int DefaultIssuePageSize = 20;
        public const int DefaultRecentIssueCount = 5;
        public const int DefaultRecentScanCount = 10;

        // Issue browsing page size options.
        public const int IssuePageSizeSmall = 10;
        public const int IssuePageSizeMedium = 25;
        public const int IssuePageSizeLarge = 50;
        public const int IssuePageSizeXLarge = 100;
        public const int IssueDefaultPageSize = IssuePageSizeMedium;
        public const int MinimumIssuePageSize = 1;
        public const int MaximumIssuePageSize = 500;
        public const int MaximumIssuePageSizeOptionCount = 10;
        public const string IssuePageSizeOptionsSeparator = ",";

        // UI and MVC request keys and route values.
        public const string BreadcrumbsViewDataKey = "Breadcrumbs";
        public const string SuccessMessageTempDataKey = "SuccessMessage";
        public const string ErrorMessageTempDataKey = "ErrorMessage";
        public const string DashboardControllerName = "Dashboard";
        public const string IssuesControllerName = "Issues";
        public const string DashboardDataActionName = "Data";
        public const string DashboardContentViewName = "_DashboardContent";
        public const int DashboardRefreshIntervalMilliseconds = 1000;
        public const string EngineFormViewName = "EngineForm";
        public const string ReviewTypeFormViewName = "ReviewTypeForm";
        public const string DashboardErrorRoute = "/Dashboard/Error";
        public const string DefaultRouteName = "default";
        public const string DefaultRoutePattern = "{controller=Dashboard}/{action=Index}/{id?}";

        // Text separators and path markers used by issue validation.
        public const string ListSeparator = ", ";
        public const string SpaceSeparator = " ";
        public const string UnderscoreText = "_";
        public const string HyphenText = "-";
        public const char PercentSign = '%';
        public const char Backslash = '\\';
        public const string CurrentDirectoryPrefix = "./";
        public const string CodeFence = "```";

        // Issue validation components and operations.
        public const string IssueValidationServiceComponent = "IssueValidationService";
        public const string IssueValidationProcessorComponent = "IssueValidationProcessor";
        public const string IssueValidationWorkerServiceComponent = "IssueValidationWorkerService";
        public const string OpenAICompletionClientComponent = "OpenAICompletionClient";
        public const string QueueValidationOperation = "QueueValidation";
        public const string ProcessValidationOperation = "ProcessValidation";
        public const string CompleteOperation = "Complete";

        // Issue validation worker timing.
        public const int IssueValidationPollIntervalSeconds = 5;
        public const int IssueValidationDrainLimit = 20;
        public const int IssueValidationStatusPollMilliseconds = 3000;

        // Duplicate detection configuration.
        public const double DefaultIssueSimilarityThreshold = 0.80;
        public const int DefaultIssueSimilarityMinimumTokenLength = 3;
        public const double IssueSimilarityMinimumScore = 0.0;
        public const double IssueSimilarityPerfectScore = 1.0;
        public const int MinimumIssueSimilarityThresholdPercent = 1;
        public const int MaximumIssueSimilarityThresholdPercent = 100;
        public const double IssueSimilarityThresholdPercentageScale = 100.0;
        public const int MinimumIssueSimilarityTokenLength = 1;
        public const int MaximumIssueSimilarityTokenLength = 20;

        // Duplicate detection components and operations.
        public const string IssueSimilarityServiceComponent = "IssueSimilarityService";
        public const string DuplicateDetectionProcessorComponent = "DuplicateDetectionProcessor";
        public const string DuplicateDetectionWorkerServiceComponent = "DuplicateDetectionWorkerService";
        public const string QueueDuplicateDetectionOperation = "QueueDuplicateDetection";
        public const string ProcessDuplicateDetectionOperation = "ProcessDuplicateDetection";

        // Duplicate detection worker timing.
        public const int DuplicateDetectionPollIntervalSeconds = 5;
        public const int DuplicateDetectionDrainLimit = 20;

        // Review engine defaults and limits.
        public const string DefaultReviewEngineEndpoint = "https://api.openai.com/v1";
        public const int DefaultReviewEngineMaxTokens = 8192;
        public const double MinimumTemperature = 0.0;
        public const double MaximumTemperature = 2.0;
        public const int ConnectionTestMaxTokens = 256;
        public const string ConnectionTestSystemPrompt = "You are a connectivity check. Reply with the single word OK.";
        public const string ConnectionTestUserPrompt = "Reply with OK.";
        public const string ReasoningEffortMinimal = "minimal";
        public const string ReasoningEffortLow = "low";
        public const string ReasoningEffortMedium = "medium";
        public const string ReasoningEffortHigh = "high";

        // Chat completion request keys.
        public const string RequestKeyModel = "model";
        public const string RequestKeyMessages = "messages";
        public const string RequestKeyResponseFormat = "response_format";
        public const string RequestKeyMaxCompletionTokens = "max_completion_tokens";
        public const string RequestKeyTemperature = "temperature";
        public const string RequestKeyReasoningEffort = "reasoning_effort";
        public const string RequestKeyStream = "stream";
        public const string ResponseFormatKeyType = "type";
        public const string MessageKeyRole = "role";
        public const string MessageKeyContent = "content";

        // Validation response keys.
        public const string ValidationKeyVerdict = "verdict";
        public const string ValidationKeyConfidence = "confidence";
        public const string ValidationKeySummary = "summary";
        public const string ValidationKeyReasoning = "reasoning";
        public const string ValidationKeySeverity = "severity";
        public const string ValidationKeyRecommended = "recommended";
        public const string ValidationKeyEvidence = "evidence";
        public const string ValidationKeyFile = "file";
        public const string ValidationKeyLineStart = "lineStart";
        public const string ValidationKeyLineEnd = "lineEnd";
        public const string ValidationKeyExplanation = "explanation";
        public const string ValidationKeySuggestions = "suggestions";
        public const string ValidationKeyLimitations = "limitations";

        // Prompt management.
        public const string SystemActor = "System";
        public const string UnknownUser = "Unknown";
        public const int PromptInitialVersionNumber = 1;
        public const string PromptSystemDisplayKey = "System";

        // Prompt components and operations.
        public const string PromptManagementServiceComponent = "PromptManagementService";
        public const string PromptResolutionServiceComponent = "PromptResolutionService";
        public const string PromptOptimisationServiceComponent = "PromptOptimisationService";
        public const string PromptOptimisationProcessorComponent = "PromptOptimisationProcessor";
        public const string PromptOptimisationWorkerServiceComponent = "PromptOptimisationWorkerService";
        public const string PromptTestRunServiceComponent = "PromptTestRunService";
        public const string PromptTestRunProcessorComponent = "PromptTestRunProcessor";
        public const string PromptTestRunWorkerServiceComponent = "PromptTestRunWorkerService";
        public const string PromptStatisticsServiceComponent = "PromptStatisticsService";
        public const string QueueOptimisationOperation = "QueueOptimisation";
        public const string ProcessOptimisationOperation = "ProcessOptimisation";
        public const string AnalysePromptOperation = "AnalysePrompt";
        public const string GeneratePromptProposalOperation = "GeneratePromptProposal";
        public const string QueueTestRunOperation = "QueueTestRun";
        public const string ProcessTestRunOperation = "ProcessTestRun";
        public const string ActivatePromptOperation = "ActivatePrompt";
        public const string CreatePromptDraftOperation = "CreatePromptDraft";
        public const string ComputeStatisticsOperation = "ComputeStatistics";
        public const string PromptSeedSystemIdentityMessage = "Seeded system prompt identity.";
        public const string PromptSeedReviewIdentityMessage = "Seeded review prompt identity for review type {0}.";
        public const string PromptSeedSystemVersionMessage = "Seeded initial system prompt version.";
        public const string PromptSeedReviewVersionMessage = "Seeded initial review prompt version for review type {0}.";

        // Prompt worker timing.
        public const int PromptOptimisationPollIntervalSeconds = 5;
        public const int PromptOptimisationDrainLimit = 10;
        public const int PromptTestRunPollIntervalSeconds = 5;
        public const int PromptTestRunDrainLimit = 10;

        // Prompt analysis response keys.
        public const string PromptAnalysisKeyAnalysisSummary = "analysisSummary";
        public const string PromptAnalysisKeyDisagreementCause = "disagreementCause";
        public const string PromptAnalysisKeySystemPromptRecommendation = "systemPromptRecommendation";
        public const string PromptAnalysisKeyReviewPromptRecommendation = "reviewPromptRecommendation";
        public const string PromptAnalysisKeyAdditionalObservations = "additionalObservations";
        public const string PromptRecommendationKeyRequired = "required";
        public const string PromptRecommendationKeyExplanation = "explanation";
        public const string PromptRecommendationKeyOriginalPrompt = "originalPrompt";
        public const string PromptRecommendationKeyProposedPrompt = "proposedPrompt";
        public const string PromptRecommendationKeyChangesSummary = "changesSummary";
        public const string PromptRecommendationKeyExpectedBenefit = "expectedBenefit";
        public const string PromptRecommendationKeyPotentialDrawbacks = "potentialDrawbacks";
        public const string PromptProposalKeySystemPrompt = "systemPrompt";
        public const string PromptProposalKeyReviewPrompt = "reviewPrompt";

        // Architecture pass.
        public const string ArchitectureSummaryServiceComponent = "ArchitectureSummaryService";
        public const string ArchitectureAnalysisOperation = "AnalyseStructure";
        public const string GetArchitectureOperation = "GetOrCreate";
        public const string ArchitectureAnalysisVersion = "1.0";
        public const string ArchitectureAnalysisFailureViewName = "ArchitectureAnalysisFailures";

        // Severity override rule configuration.
        public const string SeverityRuleFormViewName = "SeverityRuleForm";
        public const int MinimumSeverityRuleOrder = 0;
        public const int MaximumSeverityRuleOrder = 100000;
        public const int DefaultSeverityRuleOrder = 10;
        public const int SeverityRuleOrderStep = 10;
        public const int DefaultSeverityRuleSeedCount = 2;
        public const string DefaultSeverityRuleTypoName = "Typo";
        public const string DefaultSeverityRuleTypoMatchText = "typo";
        public const string DefaultSeverityRuleNamingInconsistencyName = "Naming inconsistency";
        public const string DefaultSeverityRuleNamingInconsistencyMatchText = "naming inconsistency";
    }
}
