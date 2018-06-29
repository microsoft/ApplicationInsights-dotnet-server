namespace Microsoft.ApplicationInsights.W3C
{
    using System;
    using System.ComponentModel;

    /// <summary>
    /// W3C constants.
    /// </summary>
    [Obsolete("Not ready for public consumption.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class W3CConstants
    {
        /// <summary>
        /// W3C traceparent header name.
        /// </summary>
        public const string TraceParentHeader = "traceparent";

        /// <summary>
        /// W3C tracestate header name.
        /// </summary>
        public const string TraceStateHeader = "tracestate";

        /// <summary>
        /// Trace-Id tag name.
        /// </summary>
        public const string TraceIdTag = "w3c_traceId";

        /// <summary>
        /// Span-Id tag name.
        /// </summary>
        public const string SpanIdTag = "w3c_spanId";

        /// <summary>
        /// Parent span-Id tag name.
        /// </summary>
        public const string ParentSpanIdTag = "w3c_parentSpanId";

        /// <summary>
        /// Version tag name.
        /// </summary>
        public const string VersionTag = "w3c_version";

        /// <summary>
        /// Sampled tag name.
        /// </summary>
        public const string SampledTag = "w3c_sampled";

        /// <summary>
        /// TraceState tag name.
        /// </summary>
        public const string TraceStateTag = "w3c_traceState";

        /// <summary>
        /// Default version value.
        /// </summary>
        internal const string DefaultVersion = "00";

        /// <summary>
        /// Default sampled flag value.
        /// </summary>
        internal const string DefaultSampled = "01";

        /// <summary>
        /// Name of the environment variable that controls W3C distributed tracing support.
        /// </summary>
        internal const string EnableW3CHeadersEnvironmentVariable = "APPLICATIONINSIGHTS_ENABLE_W3C_TRACING";

        /// <summary>
        /// Name of the field that carry ApplicationInsights application Id in the tracestate header.
        /// </summary>
        internal const string ApplicationIdTraceStateField = "msappid";

        /// <summary>
        /// Determines if W3C tracing is enabled.
        /// </summary>
        /// <returns>True if enabled, false otherwise.</returns>
        public static bool IsW3CTracingEnabled()
        {
            string w3CEnabledStr = Environment.GetEnvironmentVariable(EnableW3CHeadersEnvironmentVariable);
            if (bool.TryParse(w3CEnabledStr, out bool w3CEnabled))
            {
                return w3CEnabled;
            }

            return false;
        }
    }
}
