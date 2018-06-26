namespace Microsoft.ApplicationInsights.DependencyCollector.W3C
{
    /// <summary>
    /// W3C constants.
    /// </summary>
    internal static class W3CConstants
    {
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
        public const string TraceStateTag = "w3c_tracestate";

        /// <summary>
        /// Default version value.
        /// </summary>
        public const string DefaultVersion = "00";

        /// <summary>
        /// Default sampled flag value.
        /// </summary>
        public const string DefaultSampled = "01";

        /// <summary>
        /// W3C traceparent header name.
        /// </summary>
        public const string TraceParentHeader = "traceparent";

        /// <summary>
        /// W3C tracestate header name.
        /// </summary>
        public const string TraceStateHeader = "tracestate";
    }
}
