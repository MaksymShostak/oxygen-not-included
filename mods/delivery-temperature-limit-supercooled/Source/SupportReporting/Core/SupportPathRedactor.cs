#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace DeliveryTemperatureLimit
{
    internal sealed class SupportPathRedactionRule
    {
        internal SupportPathRedactionRule(
            string pathPrefix,
            string placeholder)
        {
            PathPrefix = SupportReportCollections.RequireNonBlank(
                pathPrefix,
                nameof(pathPrefix));
            Placeholder = SupportReportCollections.RequireNonBlank(
                placeholder,
                nameof(placeholder));
        }

        internal string PathPrefix { get; }

        internal string Placeholder { get; }
    }

    internal sealed class RedactedSupportText
    {
        internal RedactedSupportText(
            string content,
            IEnumerable<string> appliedPlaceholders)
        {
            Content = content ??
                throw new ArgumentNullException(nameof(content));
            AppliedPlaceholders = SupportReportCollections.CopyStrings(
                appliedPlaceholders,
                nameof(appliedPlaceholders));
        }

        internal string Content { get; }

        internal IReadOnlyList<string> AppliedPlaceholders { get; }
    }

    internal sealed class SupportPathRedactor
    {
        private readonly IReadOnlyList<SupportPathRedactionRule> rules;
        private readonly StringComparison comparison;

        internal SupportPathRedactor(
            IEnumerable<SupportPathRedactionRule> rules,
            StringComparison comparison)
        {
            if (rules == null)
            {
                throw new ArgumentNullException(nameof(rules));
            }

            if (comparison != StringComparison.Ordinal &&
                comparison != StringComparison.OrdinalIgnoreCase)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(comparison),
                    comparison,
                    "Support path matching must use an ordinal comparison.");
            }

            var copiedRules = new List<SupportPathRedactionRule>();
            foreach (SupportPathRedactionRule rule in rules)
            {
                if (rule == null)
                {
                    throw new ArgumentException(
                        "A path-redaction rule cannot be null.",
                        nameof(rules));
                }

                for (int index = 0; index < copiedRules.Count; index++)
                {
                    if (AreEquivalentPathPrefixes(
                            copiedRules[index].PathPrefix,
                            rule.PathPrefix,
                            comparison))
                    {
                        throw new ArgumentException(
                            "Path-redaction prefixes must be unique under the selected comparison.",
                            nameof(rules));
                    }
                }

                copiedRules.Add(rule);
            }

            copiedRules.Sort(CompareRules);
            this.rules =
                new ReadOnlyCollection<SupportPathRedactionRule>(copiedRules);
            this.comparison = comparison;
        }

        internal RedactedSupportText Redact(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            string redacted = text;
            var appliedPlaceholders = new List<string>();
            for (int index = 0; index < rules.Count; index++)
            {
                SupportPathRedactionRule rule = rules[index];
                bool applied;
                redacted = ReplacePathPrefix(
                    redacted,
                    rule,
                    comparison,
                    out applied);
                if (applied)
                {
                    appliedPlaceholders.Add(rule.Placeholder);
                }
            }

            return new RedactedSupportText(
                redacted,
                appliedPlaceholders);
        }

        private static int CompareRules(
            SupportPathRedactionRule left,
            SupportPathRedactionRule right)
        {
            int lengthComparison =
                right.PathPrefix.Length.CompareTo(left.PathPrefix.Length);
            if (lengthComparison != 0)
            {
                return lengthComparison;
            }

            int placeholderComparison = string.Compare(
                left.Placeholder,
                right.Placeholder,
                StringComparison.Ordinal);
            return placeholderComparison != 0
                ? placeholderComparison
                : string.Compare(
                    left.PathPrefix,
                    right.PathPrefix,
                    StringComparison.Ordinal);
        }

        private static string ReplacePathPrefix(
            string text,
            SupportPathRedactionRule rule,
            StringComparison comparison,
            out bool applied)
        {
            StringBuilder? result = null;
            int copiedThrough = 0;
            int searchFrom = 0;
            while (searchFrom <= text.Length - rule.PathPrefix.Length)
            {
                int match = IndexOfPathPrefix(
                    text,
                    rule.PathPrefix,
                    searchFrom,
                    comparison);
                if (match < 0)
                {
                    break;
                }

                int afterMatch = match + rule.PathPrefix.Length;
                if (!IsPathBoundary(text, afterMatch, rule.PathPrefix))
                {
                    searchFrom = match + 1;
                    continue;
                }

                if (result == null)
                {
                    result = new StringBuilder(text.Length);
                }

                result.Append(text, copiedThrough, match - copiedThrough);
                result.Append(rule.Placeholder);
                copiedThrough = afterMatch;
                searchFrom = afterMatch;
            }

            if (result == null)
            {
                applied = false;
                return text;
            }

            result.Append(text, copiedThrough, text.Length - copiedThrough);
            applied = true;
            return result.ToString();
        }

        private static bool AreEquivalentPathPrefixes(
            string left,
            string right,
            StringComparison comparison)
        {
            return left.Length == right.Length &&
                MatchesPathPrefixAt(left, 0, right, comparison);
        }

        private static int IndexOfPathPrefix(
            string text,
            string prefix,
            int searchFrom,
            StringComparison comparison)
        {
            int lastCandidate = text.Length - prefix.Length;
            for (int candidate = searchFrom;
                candidate <= lastCandidate;
                candidate++)
            {
                if (MatchesPathPrefixAt(
                        text,
                        candidate,
                        prefix,
                        comparison))
                {
                    return candidate;
                }
            }

            return -1;
        }

        private static bool MatchesPathPrefixAt(
            string text,
            int candidate,
            string prefix,
            StringComparison comparison)
        {
            int segmentStart = 0;
            while (segmentStart < prefix.Length)
            {
                if (IsPathSeparator(prefix[segmentStart]))
                {
                    if (!IsPathSeparator(text[candidate + segmentStart]))
                    {
                        return false;
                    }

                    segmentStart++;
                    continue;
                }

                int segmentEnd = segmentStart + 1;
                while (segmentEnd < prefix.Length &&
                    !IsPathSeparator(prefix[segmentEnd]))
                {
                    segmentEnd++;
                }

                int segmentLength = segmentEnd - segmentStart;
                if (string.Compare(
                        text,
                        candidate + segmentStart,
                        prefix,
                        segmentStart,
                        segmentLength,
                        comparison) != 0)
                {
                    return false;
                }

                segmentStart = segmentEnd;
            }

            return true;
        }

        private static bool IsPathBoundary(
            string text,
            int afterMatch,
            string prefix)
        {
            if (afterMatch == text.Length ||
                prefix[prefix.Length - 1] == '\\' ||
                prefix[prefix.Length - 1] == '/')
            {
                return true;
            }

            char next = text[afterMatch];
            return IsPathSeparator(next) ||
                char.IsWhiteSpace(next) ||
                next == '"' ||
                next == '\'' ||
                next == '(' ||
                next == ')' ||
                next == '[' ||
                next == ']' ||
                next == '{' ||
                next == '}' ||
                next == '<' ||
                next == '>' ||
                next == ',' ||
                next == ';' ||
                next == '|';
        }

        private static bool IsPathSeparator(char value)
        {
            return value == '\\' || value == '/';
        }
    }
}
