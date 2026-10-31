using System.Text.Json;

using Revathryn.Contracts;
using Revathryn.Contracts.Enums;
using Revathryn.Contracts.Reliability;

namespace Revathryn.Engine.Reliability
{
    /// <summary>Maps exceptions to failure categories for consistent handling.</summary>
    public sealed class FailureClassifier : IFailureClassifier
    {
        /// <summary>Classifies the supplied exception.</summary>
        public FailureCategory Classify(Exception exception)
        {
            if (exception is null)
            {
                return FailureCategory.Unexpected;
            }

            if (exception is ReliabilityException reliabilityException)
            {
                return reliabilityException.Category;
            }

            if (exception is TaskCanceledException taskCanceledException && !taskCanceledException.CancellationToken.IsCancellationRequested)
            {
                return FailureCategory.Transient;
            }

            if (exception is TimeoutException or HttpRequestException or IOException)
            {
                return FailureCategory.Transient;
            }

            if (exception is UnauthorizedAccessException or DirectoryNotFoundException or FileNotFoundException)
            {
                return FailureCategory.Environmental;
            }

            if (exception is InvalidDataException or JsonException or FormatException)
            {
                return FailureCategory.DataIntegrity;
            }

            if (exception is ArgumentException or InvalidOperationException or NotSupportedException or NotImplementedException)
            {
                return FailureCategory.Permanent;
            }

            if (IsSqliteException(exception))
            {
                return ClassifySqlite(exception);
            }

            return FailureCategory.Unexpected;
        }

        /// <summary>Whether the exception represents a retryable transient failure.</summary>
        public bool IsRetryable(Exception exception)
        {
            return Classify(exception) is FailureCategory.Transient;
        }

        private static bool IsSqliteException(Exception exception)
        {
            return string.Equals(exception.GetType().Name, Consts.SqliteExceptionTypeName, StringComparison.Ordinal);
        }

        private static FailureCategory ClassifySqlite(Exception exception)
        {
            object errorCode = exception.GetType().GetProperty(Consts.SqliteErrorCodePropertyName)?.GetValue(exception);

            if (errorCode is int code && IsTransientSqliteError(code))
            {
                return FailureCategory.Transient;
            }

            return FailureCategory.DataIntegrity;
        }

        private static bool IsTransientSqliteError(int errorCode)
        {
            return errorCode is Consts.SqliteBusyErrorCode or Consts.SqliteLockedErrorCode or Consts.SqliteBusySnapshotErrorCode;
        }
    }
}
