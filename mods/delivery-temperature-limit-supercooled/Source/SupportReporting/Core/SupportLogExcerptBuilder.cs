#nullable enable

using System;
using System.IO;
using System.Text;

namespace DeliveryTemperatureLimit
{
    internal sealed class SupportLogExcerptBuilder
    {
        private static readonly Encoding NonThrowingUtf8 =
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: false);

        internal SupportPlayerLogSnapshot Create(
            Stream seekableLog,
            string sourceState,
            SupportPathRedactor redactor)
        {
            if (seekableLog == null)
            {
                throw new ArgumentNullException(nameof(seekableLog));
            }

            if (!seekableLog.CanRead || !seekableLog.CanSeek)
            {
                throw new ArgumentException(
                    "The player-log stream must be readable and seekable.",
                    nameof(seekableLog));
            }

            string validatedSourceState =
                SupportReportCollections.RequireNonBlank(
                    sourceState,
                    nameof(sourceState));
            if (redactor == null)
            {
                throw new ArgumentNullException(nameof(redactor));
            }

            long originalByteCount = seekableLog.Length;
            if (originalByteCount < 0)
            {
                throw new IOException(
                    "The player-log stream reported a negative length.");
            }

            long start = Math.Max(
                0,
                originalByteCount -
                    SupportReportLimits.MaximumRawPlayerLogBytes);
            bool rawTailWasTruncated = start > 0;
            int requestedByteCount = checked((int)Math.Min(
                SupportReportLimits.MaximumRawPlayerLogBytes,
                originalByteCount - start));
            seekableLog.Seek(start, SeekOrigin.Begin);

            byte[] bytes = new byte[requestedByteCount];
            int includedRawByteCount = ReadToEnd(
                seekableLog,
                bytes,
                requestedByteCount);
            string decoded = NonThrowingUtf8.GetString(
                bytes,
                0,
                includedRawByteCount);
            if (rawTailWasTruncated &&
                decoded.Length > 0 &&
                decoded[0] == '\uFFFD')
            {
                decoded = decoded.Substring(1);
            }

            RedactedSupportText redacted = redactor.Redact(decoded);
            bool escapedContentWasTruncated;
            string boundedContent = KeepNewestJsonEscapedContent(
                redacted.Content,
                SupportReportLimits.MaximumEscapedPlayerLogBytes,
                out escapedContentWasTruncated);

            return SupportPlayerLogSnapshot.Available(
                validatedSourceState,
                originalByteCount,
                includedRawByteCount,
                rawTailWasTruncated || escapedContentWasTruncated,
                redacted.AppliedPlaceholders,
                boundedContent);
        }

        private static int ReadToEnd(
            Stream source,
            byte[] buffer,
            int requestedByteCount)
        {
            int totalRead = 0;
            while (totalRead < requestedByteCount)
            {
                int read = source.Read(
                    buffer,
                    totalRead,
                    requestedByteCount - totalRead);
                if (read == 0)
                {
                    break;
                }

                totalRead += read;
            }

            return totalRead;
        }

        internal static string KeepNewestJsonEscapedContent(
            string content,
            int maximumEscapedByteCount,
            out bool truncated)
        {
            long escapedByteCount = GetJsonEscapedUtf8ByteCount(content);
            if (escapedByteCount <= maximumEscapedByteCount)
            {
                truncated = false;
                return content;
            }

            int start = content.Length;
            long retainedEscapedByteCount = 0;
            while (start > 0)
            {
                int scalarStart = start - 1;
                if (char.IsLowSurrogate(content[scalarStart]) &&
                    scalarStart > 0 &&
                    char.IsHighSurrogate(content[scalarStart - 1]))
                {
                    scalarStart--;
                }

                int scalarByteCount = GetJsonEscapedUtf8ByteCount(
                    content,
                    scalarStart,
                    start - scalarStart);
                if (retainedEscapedByteCount + scalarByteCount >
                    maximumEscapedByteCount)
                {
                    break;
                }

                retainedEscapedByteCount += scalarByteCount;
                start = scalarStart;
            }

            truncated = true;
            return content.Substring(start);
        }

        private static long GetJsonEscapedUtf8ByteCount(string content)
        {
            long byteCount = 0;
            for (int index = 0; index < content.Length; index++)
            {
                int characterCount =
                    char.IsHighSurrogate(content[index]) &&
                    index + 1 < content.Length &&
                    char.IsLowSurrogate(content[index + 1])
                        ? 2
                        : 1;
                byteCount += GetJsonEscapedUtf8ByteCount(
                    content,
                    index,
                    characterCount);
                index += characterCount - 1;
            }

            return byteCount;
        }

        private static int GetJsonEscapedUtf8ByteCount(
            string content,
            int start,
            int characterCount)
        {
            char character = content[start];
            switch (character)
            {
                case '"':
                case '\\':
                case '\b':
                case '\t':
                case '\n':
                case '\f':
                case '\r':
                    return 2;
                case '\u0085':
                case '\u2028':
                case '\u2029':
                    return 6;
            }

            if (character < 0x20)
            {
                return 6;
            }

            if (characterCount == 2)
            {
                return 4;
            }

            if (char.IsSurrogate(character))
            {
                return 6;
            }

            if (character <= 0x7F)
            {
                return 1;
            }

            return character <= 0x7FF ? 2 : 3;
        }
    }
}
