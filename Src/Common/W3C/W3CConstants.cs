#if DEPENDENCY_COLLECTOR
    namespace Microsoft.ApplicationInsights.W3C
#else
    namespace Microsoft.ApplicationInsights.W3C.Internal
#endif
{
    using System.ComponentModel;

    /// <summary>
    /// W3C constants.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
#if DEPENDENCY_COLLECTOR
    public
#else
    internal
#endif
    static class W3CConstants
    {
        /// <summary>
        /// W3C traceparent header name.
        /// </summary>
        public const string TraceParentHeader = "traceparent";

        /// <summary>
        /// W3C tracestate header name.
        /// </summary>
        public const string TraceStateHeader = "tracestate";

        internal const string LegacyRootPropertyIdKey = "ai_legacyRootId";
        internal const string TracestatePropertyKey = "tracestate";
    }
}
