namespace Microsoft.ApplicationInsights.Web.Extensibility.Implementation
{
    using System;

    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.Common;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.Implementation.Tracing;
    using Microsoft.ApplicationInsights.Web.Implementation;

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
                var context = System.Web.HttpContext.Current;
                if (requestTelemetry.Url == null)
                {
                    requestTelemetry.Url = context.Request.UnvalidatedGetUrl();
                }

                var headers = context.Request.UnvalidatedGetHeaders();
                if (string.IsNullOrEmpty(requestTelemetry.Source) && headers != null)
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

                    string currentComponentAppId = null;
                    if (!string.IsNullOrEmpty(requestTelemetry.Context.InstrumentationKey)
                        && (this.TelemetryConfiguration?.ApplicationIdProvider?.TryGetApplicationId(requestTelemetry.Context.InstrumentationKey, out currentComponentAppId) ?? false))
                    {
                        // If the source header is present on the incoming request,
                        // and it is an external component (not the same ikey as the one used by the current component),
                        // then populate the source field.
                        if (!string.IsNullOrEmpty(sourceAppId) && sourceAppId != currentComponentAppId)
                        {
                            requestTelemetry.Source = sourceAppId;
                        }
                    }
                }
            }

            this.nextProcessorInPipeline?.Process(item);
        }
    }
}
