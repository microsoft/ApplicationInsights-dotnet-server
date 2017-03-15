namespace Microsoft.ApplicationInsights.Common
{
    using System.Collections.Generic;
    using System.Globalization;

    /// <summary>
    /// Generic functions that can be used to get and set Http headers.
    /// </summary>
    public static class HeadersExtensions
    {
        /// <summary>
        /// Get the key value from the provided HttpHeader value that is set up as a comma-separated list of key value pairs. Each key value pair is formatted like (key)=(value).
        /// </summary>
        /// <param name="headerValue">The header values that may contain key name/value pairs.</param>
        /// <param name="keyName">The name of the key value to find in the provided header values.</param>
        /// <returns>The first key value, if it is found. If it is not found, then null.</returns>
        public static string GetHeaderKeyValue(IEnumerable<string> headerValue, string keyName)
        {
            if (headerValue != null)
            {
                foreach (string keyNameValue in headerValue)
                {
                    string[] keyNameValueParts = keyNameValue.Trim().Split('=');
                    if (keyNameValueParts.Length == 2 && keyNameValueParts[0].Trim() == keyName)
                    {
                        return keyNameValueParts[1].Trim();
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Given the provided list of header value strings, return a comma-separated list of key
        /// name/value pairs with the provided keyName and keyValue. If the initial header value
        /// strings contains the key name, then the original key value should be replaced with the
        /// provided key value. If the initial header value strings don't contain the key name,
        /// then the key name/value pair should be added to the comma-separated list and returned.
        /// </summary>
        /// <param name="headerValues">The existing header values that the key/value pair should be added to.</param>
        /// <param name="keyName">The name of the key to add.</param>
        /// <param name="keyValue">The value of the key to add.</param>
        /// <returns>The result of setting the provided key name/value pair into the provided headerValues.</returns>
        public static string SetHeaderKeyValue(IEnumerable<string> headerValues, string keyName, string keyValue)
        {
            string result = string.Empty;
            bool found = false;

            string keyValueStringToAdd = string.Format(CultureInfo.InvariantCulture, "{0}={1}", keyName.Trim(), keyValue.Trim());

            if (headerValues != null)
            {
                foreach (string keyValuePair in headerValues)
                {
                    string[] keyValueParts = keyValuePair.Split('=');
                    if (keyValueParts.Length != 2 || keyValueParts[0].Trim() != keyName)
                    {
                        result = Append(result, keyValuePair);
                    }
                    else if (!found)
                    {
                        found = true;
                        result = Append(result, keyValueStringToAdd);
                    }
                }
            }

            if (!found)
            {
                result = Append(result, keyValueStringToAdd);
            }

            return result;
        }

        /// <summary>
        /// Append the provided toAppend string after the provided value string.
        /// </summary>
        /// <param name="value">The string to append toAppend to.</param>
        /// <param name="toAppend">The string to append after value.</param>
        /// <returns>The result of appending toAppend to value with a comma a space separating them.</returns>
        private static string Append(string value, string toAppend)
        {
            string result;

            if (!string.IsNullOrEmpty(value))
            {
                result = value.Trim();
                if (!string.IsNullOrEmpty(toAppend))
                {
                    result += ", " + toAppend.Trim();
                }
            }
            else if (!string.IsNullOrEmpty(toAppend))
            {
                result = toAppend.Trim();
            }
            else
            {
                result = string.Empty;
            }

            return result;
        }
    }
}
