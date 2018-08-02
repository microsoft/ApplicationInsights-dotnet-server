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
        /// Name of the field that carry ApplicationInsights application Id in the tracestate header.
        /// </summary>
        public const string ApplicationIdTraceStateField = "msappid";

        /// <summary>
        /// Trace-Id tag name.
        /// </summary>
        internal const string TraceIdTag = "w3c_traceId";

        /// <summary>
        /// Span-Id tag name.
        /// </summary>
        internal const string SpanIdTag = "w3c_spanId";

        /// <summary>
        /// Parent span-Id tag name.
        /// </summary>
        internal const string ParentSpanIdTag = "w3c_parentSpanId";

        /// <summary>
        /// Version tag name.
        /// </summary>
        internal const string VersionTag = "w3c_version";

        /// <summary>
        /// Sampled tag name.
        /// </summary>
        internal const string SampledTag = "w3c_sampled";

        /// <summary>
        /// TraceState tag name.
        /// </summary>
        internal const string TraceStateTag = "w3c_traceState";

        /// <summary>
        /// Default version value.
        /// </summary>
        internal const string DefaultVersion = "00";

        /// <summary>
        /// Default sampled flag value.
        /// </summary>
        internal const string DefaultSampled = "00";

        /// <summary>
        /// Legacy root Id tag name.
        /// </summary>
        internal const string LegacyRootIdProperty = "ai_legacyRootId";

        /// <summary>
        /// Legacy root Id tag name.
        /// </summary>
        internal const string LegacyRequestIdProperty = "ai_legacyParentId";
    }
}
