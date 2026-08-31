#nullable enable

using System;
using System.Text;

namespace DeliveryTemperatureLimit
{
    internal sealed class SupportJsonReportSerialization
    {
        internal SupportJsonReportSerialization(
            SupportReportDocument document,
            string json,
            int utf8ByteCount)
        {
            Document = document ??
                throw new ArgumentNullException(nameof(document));
            Json = json ?? throw new ArgumentNullException(nameof(json));
            Utf8ByteCount = utf8ByteCount;
        }

        public SupportReportDocument Document { get; }

        public string Json { get; }

        public int Utf8ByteCount { get; }
    }

    internal static class SupportJsonReportSizeLimiter
    {
        private const string FurtherShorteningWarning =
            "Player.log content was shortened further to keep the complete " +
            "report below 12 MiB.";

        private static readonly Encoding Utf8WithoutBom =
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true);

        internal static SupportJsonReportSerialization SerializeWithinLimit(
            SupportReportDocument document,
            int maximumByteCount,
            Func<SupportReportDocument, string> serialize)
        {
            if (document == null)
            {
                throw new ArgumentNullException(nameof(document));
            }

            if (maximumByteCount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumByteCount));
            }

            if (serialize == null)
            {
                throw new ArgumentNullException(nameof(serialize));
            }

            string originalJson = RequireSerializedJson(
                serialize(document));
            int originalByteCount = Utf8WithoutBom.GetByteCount(
                originalJson);
            if (originalByteCount < maximumByteCount)
            {
                return new SupportJsonReportSerialization(
                    document,
                    originalJson,
                    originalByteCount);
            }

            SupportPlayerLogSnapshot? playerLog = document.PlayerLog;
            if (playerLog == null ||
                !string.Equals(
                    playerLog.State,
                    SupportReportLimits.AvailableState,
                    StringComparison.Ordinal) ||
                playerLog.Content == null)
            {
                throw CreateSizeLimitException();
            }

            SupportReportDocument emptyLogDocument =
                document.WithFurtherShortenedPlayerLog(
                    string.Empty,
                    FurtherShorteningWarning);
            string emptyLogJson = RequireSerializedJson(
                serialize(emptyLogDocument));
            int emptyLogByteCount = Utf8WithoutBom.GetByteCount(
                emptyLogJson);
            if (emptyLogByteCount >= maximumByteCount)
            {
                throw CreateSizeLimitException();
            }

            int escapedContentBudget =
                maximumByteCount - emptyLogByteCount - 1;
            bool contentWasShortened;
            string boundedContent =
                SupportLogExcerptBuilder.KeepNewestJsonEscapedContent(
                    playerLog.Content,
                    escapedContentBudget,
                    out contentWasShortened);
            if (!contentWasShortened)
            {
                throw CreateSizeLimitException();
            }

            SupportReportDocument boundedDocument =
                document.WithFurtherShortenedPlayerLog(
                    boundedContent,
                    FurtherShorteningWarning);
            string boundedJson = RequireSerializedJson(
                serialize(boundedDocument));
            int boundedByteCount = Utf8WithoutBom.GetByteCount(boundedJson);
            if (boundedByteCount >= maximumByteCount)
            {
                throw CreateSizeLimitException();
            }

            return new SupportJsonReportSerialization(
                boundedDocument,
                boundedJson,
                boundedByteCount);
        }

        private static string RequireSerializedJson(string? json)
        {
            if (json == null)
            {
                throw new InvalidOperationException(
                    "The support report serializer returned no JSON.");
            }

            return json;
        }

        private static InvalidOperationException CreateSizeLimitException() =>
            new InvalidOperationException(
                "The generated support report reached the 12 MiB safety limit.");
    }
}
