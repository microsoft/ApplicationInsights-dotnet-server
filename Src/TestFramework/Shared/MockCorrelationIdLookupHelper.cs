namespace Microsoft.ApplicationInsights.Web.TestFramework
{
    using System.Globalization;
    using Microsoft.ApplicationInsights.Common.CorrelationLookup;

    /// <summary>
    /// Use this to have TryGet return the instrumentation key as the correlation id.
    /// </summary>
    internal class MockCorrelationIdLookupHelper : ICorrelationIdLookupHelper
    {
        public static string GetCorrelationIdValue(string instrumentationKey)
        {
            return string.Format(CultureInfo.InvariantCulture, "cid-v1:{0}-appId", instrumentationKey);
        }

        public bool TryGetXComponentCorrelationId(string instrumentationKey, out string correlationId)
        {
            correlationId = GetCorrelationIdValue(instrumentationKey);
            return true;
        }
    }
}
