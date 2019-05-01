namespace Microsoft.ApplicationInsights.Web.Extensibility.Implementation
{
    using System.Web;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.Common;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;

    /// <summary>
    /// [This feature is still being evaluated and not recommended for end users.]
    /// This class is intended to be used with <see cref="RequestTrackingTelemetryModule.DisableTrackingProperties"/> for deferring execution until after Sampling.
    /// This processor RequestTelemetry.Url is initialized in the context of HttpContext.Current.Request
    /// </summary>
    public class PostSamplingTelemetryProcessor : ITelemetryProcessor
    {
        private readonly ITelemetryProcessor nextProcessorInPipeline;
        private TelemetryConfiguration telemetryConfiguration;

        /// <summary>
        /// Initializes a new instance of the <see cref="PostSamplingTelemetryProcessor"/> class.
        /// </summary>
        /// <param name="nextProcessorInPipeline">The next TelemetryProcessor in the chain.</param>
        public PostSamplingTelemetryProcessor(ITelemetryProcessor nextProcessorInPipeline)
        {
            this.nextProcessorInPipeline = nextProcessorInPipeline;
        }

        private TelemetryConfiguration TelemetryConfiguration => this.telemetryConfiguration ?? (this.telemetryConfiguration = TelemetryConfiguration.Active);

        /// <inheritdoc />
        public void Process(ITelemetry item)
        {
            if (item is RequestTelemetry requestTelemetry)
            {
                var request = HttpContext.Current?.Request;
                RequestTrackingUtilities.UpdateRequestTelemetryFromRequest(requestTelemetry, request, this.telemetryConfiguration);
            }

            this.nextProcessorInPipeline?.Process(item);
        }
    }
}
