namespace Microsoft.ApplicationInsights.Common
{
    using System;
    using System.Diagnostics;
#if !NET45
    using static System.FormattableString;
#endif

    /// <summary>
    /// Generic functions to perform common operations on a string.
    /// </summary>
    public static class StringUtilities
    {
        /// <summary>
        /// Check a strings length and trim to a max length if needed.
        /// </summary>
        public static string EnforceMaxLength(string input, int maxLength)
        {
            Debug.Assert(input != null, Invariant($"{nameof(input)} must not be null"));
            Debug.Assert(maxLength > 0, Invariant($"{nameof(maxLength)} must be greater than 0"));

            if (input != null && input.Length > maxLength)
            {
                input = input.Substring(0, maxLength);
            }

            return input;
        }

#if NET45
        /// <summary>
        /// Format the given object in the invariant culture. This static method may be
        /// imported in C# by
        /// <code>
        /// using static System.FormattableString;
        /// </code>.
        /// Within the scope
        /// of that import directive an interpolated string may be formatted in the
        /// invariant culture by writing, for example,
        /// <code>
        /// Invariant($"{{ lat = {latitude}; lon = {longitude} }}")
        /// </code>
        /// </summary>
        internal static string Invariant(IFormattable formattable)
        {
            // This was added to Net Framework in 4.6
            // Borrowed from: https://referencesource.microsoft.com/#mscorlib/system/FormattableString.cs
            if (formattable == null)
            {
                throw new ArgumentNullException("formattable");
            }

            return formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture);
        }
#endif
    }
}
