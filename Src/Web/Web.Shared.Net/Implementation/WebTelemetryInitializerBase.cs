namespace Microsoft.ApplicationInsights.Web.Implementation
{
    using System;
    using System.Web;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.Implementation.Tracing;

    /// <summary>
    /// Base class for WebOperationTelemetryInitializers.
    /// </summary>
    public abstract class WebTelemetryInitializerBase : ITelemetryInitializer
    {
        internal WebTelemetryInitializerBase()
        {
            WebEventSource.Log.WebTelemetryInitializerLoaded(this.GetType().FullName);
        }

        /// <summary>
        /// Base implementation of the initialization method.
        /// </summary>
        /// <param name="telemetry">Telemetry item to initialize.</param>
        public void Initialize(ITelemetry telemetry)
        {
            try
            {
                var platformContext = this.ResolvePlatformContext();

                if (platformContext == null)
                {
                    WebEventSource.Log.WebTelemetryInitializerNotExecutedOnNullHttpContext();
                    return;
                }

                if (platformContext.GetRequest() == null)
                {
                    return;
                }

                var operationHolder = platformContext.GetOperation();
                if (operationHolder != null)
                {
                    //If OnBeginRequest was called, there will always be a RequestTelemetry operation holder in the HttpContext
                    //If it was not called, and request failed before reaching AppInsights module, then we came here from the OnEndRequest
                    //That also creates RequestTelemetry operation holder in the HttpContext if it's missing
                    this.OnInitializeTelemetry(platformContext, operationHolder.Telemetry, telemetry);
                }
                else if (telemetry is RequestTelemetry)
                {
                    //However we initialize RequestTelemetry BEFORE it was stored in the HttpContext
                    //so we are dealing with RequestTelemetry itself here
                    this.OnInitializeTelemetry(platformContext, (RequestTelemetry) telemetry, telemetry);
                }
            }
            catch (Exception exc)
            {
                WebEventSource.Log.WebTelemetryInitializerFailure(
                    this.GetType().FullName, 
                    exc.ToInvariantString());
            }
        }

        /// <summary>
        /// Implements initialization logic.
        /// </summary>
        /// <param name="platformContext">Http context.</param>
        /// <param name="requestTelemetry">Request telemetry object associated with the current request.</param>
        /// <param name="telemetry">Telemetry item to initialize.</param>
        protected abstract void OnInitializeTelemetry(
            HttpContext platformContext,
            RequestTelemetry requestTelemetry, 
            ITelemetry telemetry);

        /// <summary>
        /// Resolved web platform specific context.
        /// </summary>
        /// <returns>An instance of the context.</returns>
        protected virtual HttpContext ResolvePlatformContext()
        {
            return HttpContext.Current;
        }
    }
}
