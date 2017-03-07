namespace Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.Implementation.QuickPulse
{
    /// <summary>
    /// Constants related to quick pulse service.
    /// </summary>
    internal class QuickPulseConstants
    {
        /// <summary>
        /// Subscribed header.
        /// </summary>
        internal const string XMsQpsSubscribedHeaderName = "x-ms-qps-subscribed";

        /// <summary>
        /// Transmission time header.
        /// </summary>
        internal const string XMsQpsTransmissionTimeHeaderName = "x-ms-qps-transmission-time";

        /// <summary>
        /// Configuration ETag header.
        /// </summary>
        internal const string XMsQpsConfigurationETagHeaderName = "x-ms-qps-configuration-etag";

        /// <summary>
        /// Authentication API key.
        /// </summary>
        internal const string XMsQpsAuthApiKeyHeaderName = "x-ms-qps-auth-api-key";

        /// <summary>
        /// Instance name header.
        /// </summary>
        internal const string XMsQpsInstanceNameHeaderName = "x-ms-qps-instance-name";

        /// <summary>
        /// Stream id header.
        /// </summary>
        internal const string XMsQpsStreamIdHeaderName = "x-ms-qps-stream-id";

        /// <summary>
        /// Machine name header.
        /// </summary>
        internal const string XMsQpsMachineNameHeaderName = "x-ms-qps-machine-name";
    }
}
