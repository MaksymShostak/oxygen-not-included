#nullable enable

using System;

namespace DeliveryTemperatureLimit
{
    /// <summary>
    /// Enforces the stable identifier grammar shared by declared integrations,
    /// semantic runtime capabilities, and prepared runtime patch groups.
    /// </summary>
    internal static class ValidatedIntegrationIdentifier
    {
        internal static string RequireKebabCase(
            string value,
            string parameterName)
        {
            if (value == null)
            {
                throw new ArgumentNullException(parameterName);
            }

            if (value.Length == 0 || value.Length > 64 ||
                value[0] == '-' || value[value.Length - 1] == '-')
            {
                throw InvalidIdentifier(parameterName);
            }

            bool previousWasHyphen = false;
            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool isHyphen = character == '-';
                bool isLowerAscii = character >= 'a' && character <= 'z';
                bool isDigit = character >= '0' && character <= '9';
                if ((!isHyphen && !isLowerAscii && !isDigit) ||
                    (isHyphen && previousWasHyphen))
                {
                    throw InvalidIdentifier(parameterName);
                }

                previousWasHyphen = isHyphen;
            }

            return value;
        }

        private static ArgumentException InvalidIdentifier(
            string parameterName) =>
            new ArgumentException(
                "An integration-domain identifier must contain 1-64 " +
                "lowercase ASCII kebab-case characters.",
                parameterName);
    }
}
