namespace Microsoft.ApplicationInsights.Web
{
    using System.Web;
    using Common;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;
    using Microsoft.ApplicationInsights.Web.Implementation;

    /// <summary>
    /// A telemetry initializer that will set the correlation context for all telemetry items in web application.
    /// </summary>
    public class OperationCorrelationTelemetryInitializer : WebTelemetryInitializerBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="OperationCorrelationTelemetryInitializer"/> class.
        /// </summary>
        public OperationCorrelationTelemetryInitializer()
        {
            this.ParentOperationIdHeaderName = RequestResponseHeaders.StandardParentIdHeader;
            this.RootOperationIdHeaderName = RequestResponseHeaders.StandardRootIdHeader;
        }

        /// <summary>
        /// Gets or sets the name of the header to get parent operation Id from.
        /// </summary>
        public string ParentOperationIdHeaderName { get; set; }

        /// <summary>
        /// Gets or sets the name of the header to get root operation Id from.
        /// </summary>
        public string RootOperationIdHeaderName { get; set; }

        /// <summary>
        /// Implements initialization logic.
        /// </summary>
        /// <param name="platformContext">Http context.</param>
        /// <param name="requestTelemetry">Request telemetry object associated with the current request.</param>
        /// <param name="telemetry">Telemetry item to initialize.</param>
        protected override void OnInitializeTelemetry(
            HttpContext platformContext,
            RequestTelemetry requestTelemetry,
            ITelemetry telemetry)
        {
            OperationContext parentContext = requestTelemetry.Context.Operation;
            HttpRequest currentRequest = platformContext.Request;

            //We either have both root Id and parent Id or just rootId.
            //having single parentId is inconsistent and invalid and we'll update it.
            if (string.IsNullOrEmpty(parentContext.Id))
            {
                string rootId, parentId, requestId;
                if (
                    TryParseStandardHeader(requestTelemetry, currentRequest, out rootId, out parentId,
                        out requestId) ||
                    TryParseCustomHeaders(requestTelemetry, currentRequest, out rootId, out parentId,
                        out requestId))
                {
                    //we managed to get something from headers
                    requestTelemetry.Context.Operation.Id = rootId;
                    requestTelemetry.Context.Operation.ParentId = parentId;
                    requestTelemetry.Id = requestId;
                }
                else
                {
                    //there was nothing in the headers.
                    //Since ActivityAPI is not available on NET 4.5, we mimic it't behavior
                    //TODO: move Id generation to Base SDK
                    requestTelemetry.Id = AppInsightsActivity.GenerateNewId();
                    requestTelemetry.Context.Operation.Id = AppInsightsActivity.GetRootId(requestTelemetry.Id);
                }
            }

            if (telemetry != requestTelemetry)
            {
                if (string.IsNullOrEmpty(telemetry.Context.Operation.ParentId))
                {
                    telemetry.Context.Operation.ParentId = requestTelemetry.Id;
                }

                if (string.IsNullOrEmpty(telemetry.Context.Operation.Id))
                {
                    telemetry.Context.Operation.Id = parentContext.Id;
                }
            }
        }
        private bool TryParseStandardHeader(
            RequestTelemetry telemetry,
            HttpRequest request,
            out string rootId,
            out string parentId,
            out string requestId)
        {
            parentId = request.UnvalidatedGetHeader(RequestResponseHeaders.RequestIdHeader);
            if (!string.IsNullOrEmpty(parentId))
            {
                rootId = AppInsightsActivity.GetRootId(parentId);
                requestId = AppInsightsActivity.GenerateRequestId(parentId, telemetry.Id);
                //TODO: Correlation-Context
                return true;
            }
            rootId = null;
            requestId = null;
            return false;
        }

        private bool TryParseCustomHeaders(
            RequestTelemetry telemetry,
            HttpRequest request,
            out string rootId,
            out string parentId,
            out string requestId)
        {
            parentId = request.UnvalidatedGetHeader(this.ParentOperationIdHeaderName);
            rootId = request.UnvalidatedGetHeader(this.RootOperationIdHeaderName);
            if (!string.IsNullOrEmpty(rootId))
            {
                requestId = AppInsightsActivity.GenerateRequestId(rootId, telemetry.Id);
                return true;
            }
            if (!string.IsNullOrEmpty(parentId))
            {
                requestId = AppInsightsActivity.GenerateRequestId(parentId, telemetry.Id);
                return true;
            }

            requestId = null;
            return false;
        }
    }
}
