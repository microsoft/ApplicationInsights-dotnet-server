namespace Microsoft.ApplicationInsights.Web.Helpers
{
    using System.Web;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Web.Implementation;

    internal static class HttpContextTestExtensions
    {
        internal static RequestTelemetry WithAuthCookie(this HttpContext context, string cookieString)
        {
            context.AddRequestCookie(
                new HttpCookie(
                    RequestTrackingConstants.WebAuthenticatedUserCookieName,
                                                    HttpUtility.UrlEncode(cookieString)));
            return context.GetOperation().Telemetry;
        }

        internal static IOperationHolder<RequestTelemetry> SetOperationHolder(this HttpContext context, RequestTelemetry requestTelemetry = null)
        {
            var operationHolder = new TestOperationHolder(requestTelemetry ?? new RequestTelemetry());
            
            context.Items.Add(RequestTrackingConstants.RequestTelemetryItemName, operationHolder);
            return operationHolder;
        }
    }
}
