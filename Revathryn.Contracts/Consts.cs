namespace Revathryn.Contracts
{
    public static class Consts
    {
        public const string DefaultProgramDataPath = "Revathryn";
        public const string DefaultBackupDirectoryName = "backups";
        public const string DefaultDatabaseFileName = "revathryn.db";
        public const string DatabasePath = "db";
        public const string DefaultDataStore = "Data Source={0}";
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
        public const string JsonFileExtension = ".json";
        public const string CSharpFilePattern = Star + FullStop + FileTypeCSharp;
        public const char CharLessThan = '<';
        public const char CharGreaterThan = '>';
        public const string StringQuestionMark = "?";
        public const string Pipe = "|";
        public const char CharForwardSlash = '/';
        public const char CharBackSlash = '\\';
        public const char CharFullStop = '.';
        public const char CharI = 'I';
        public const char NewLine = '\n';
        public const char CharComma = ',';
        public const char CharLeftSquareBracket = '[';
        public const char CharRightSquareBracket = ']';
        public const char CharSpace = ' ';
        public const char CharPlus = '+';
        public const char CharHyphen = '-';
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
        public const int DefaultMaxContextRequests = 3;
        public const int MaxContextItemsPerRequest = 10;
        public const int MaxContextItemContentLength = 4000;
        public const string ContextKeySymbols = "symbols";
        public const string ContextKeyDependencies = "dependencies";
        public const string FindingFingerprintSeparator = ":";
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
        public const string LivenessCheck = "Liveness";
        public const string ReviewProcessingCheck = "ReviewProcessing";

        // Issue query defaults.
        public const int DefaultIssuePageSize = 20;
        public const int DefaultRecentIssueCount = 5;
        public const int DefaultRecentScanCount = 10;

        // UI and MVC request keys and route values.
        public const string BreadcrumbsViewDataKey = "Breadcrumbs";
        public const string SuccessMessageTempDataKey = "SuccessMessage";
        public const string ErrorMessageTempDataKey = "ErrorMessage";
        public const string DashboardControllerName = "Dashboard";
        public const string DashboardDataActionName = "Data";
        public const string DashboardContentViewName = "_DashboardContent";
        public const int DashboardRefreshIntervalMilliseconds = 1000;
        public const string EngineFormViewName = "EngineForm";
        public const string DashboardErrorRoute = "/Dashboard/Error";
        public const string DefaultRouteName = "default";
        public const string DefaultRoutePattern = "{controller=Dashboard}/{action=Index}/{id?}";
    }
}
