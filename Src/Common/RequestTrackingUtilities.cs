namespace Microsoft.ApplicationInsights.Common
{
    using System;
    using System.Collections.Specialized;
    using System.Web;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.Implementation.Tracing;

    /// <summary>
    /// RequestTrackingUtilities class.
    /// </summary>
    internal static class RequestTrackingUtilities
    {
        /// <summary>
        /// s
        /// </summary>
        /// <param name="requestTelemetry">a</param>
        /// <param name="request">b</param>
        /// <param name="telemetryConfiguration">c</param>
        public static void UpdateRequestTelemetryFromRequest(RequestTelemetry requestTelemetry, HttpRequest request, TelemetryConfiguration telemetryConfiguration)
        {
            if (requestTelemetry == null || request == null)
            {
                return;
            }

            if (requestTelemetry.Url == null)
            {
                requestTelemetry.Url = request.Unvalidated.Url;
            }

            if (string.IsNullOrEmpty(requestTelemetry.Source))
            {
                var sourceAppId = GetSourceAppId(request.Unvalidated.Headers);
                string currentComponentAppId = GetApplicationId(telemetryConfiguration, requestTelemetry.Context?.InstrumentationKey);
                // If the source header is present on the incoming request,
                // and it is an external component (not the same ikey as the one used by the current component),
                // then populate the source field.
                if (!string.IsNullOrEmpty(currentComponentAppId) &&
                    !string.IsNullOrEmpty(sourceAppId) &&
                    sourceAppId != currentComponentAppId)
                {
                    requestTelemetry.Source = sourceAppId;
                }
            }
        }

        private static string GetSourceAppId(NameValueCollection headers)
        {
            string sourceAppId = null;

            try
            {
                sourceAppId = headers.GetNameValueHeaderValue(
                    RequestResponseHeaders.RequestContextHeader,
                    RequestResponseHeaders.RequestContextCorrelationSourceKey);
            }
            catch (Exception ex)
            {
                AppMapCorrelationEventSource.Log.GetCrossComponentCorrelationHeaderFailed(ex.ToInvariantString());
            }

            return sourceAppId;
        }

        private static string GetApplicationId(TelemetryConfiguration telemetryConfiguration, string instrumentationKey)
        {
            string currentComponentAppId = null;
            telemetryConfiguration.ApplicationIdProvider?.TryGetApplicationId(instrumentationKey, out currentComponentAppId);
            return currentComponentAppId;
        }
    }
}