using Microsoft.ApplicationInsights.Common;

namespace Microsoft.ApplicationInsights.Web.Implementation
{
    using System;
    using System.Linq;
    using System.Web;
    using Microsoft.ApplicationInsights.DataContracts;
    
    internal static class RequestTrackingExtensions
    {
        internal static RequestTelemetry CreateRequestTelemetryPrivate(
            this HttpContext platformContext)
        {
            if (platformContext == null)
            {
                throw new ArgumentException("platformContext");
            }

            var result = new RequestTelemetry();
            TryParseStandardHeaders(result, platformContext.Request);

            platformContext.Items.Add(RequestTrackingConstants.RequestTelemetryItemName, result);
            WebEventSource.Log.WebTelemetryModuleRequestTelemetryCreated();

            return result;
        }

        internal static RequestTelemetry ReadOrCreateRequestTelemetryPrivate(
            this HttpContext platformContext)
        {
            if (platformContext == null)
            {
                throw new ArgumentException("platformContext");
            }

            var result = platformContext.GetRequestTelemetry() ??
                         CreateRequestTelemetryPrivate(platformContext);

            return result;
        }

        internal static string CreateRequestNamePrivate(this HttpContext platformContext)
        {
            var request = platformContext.Request;
            string name = request.UnvalidatedGetPath();

            if (request.RequestContext != null &&
                request.RequestContext.RouteData != null)
            {
                var routeValues = request.RequestContext.RouteData.Values;

                if (routeValues != null && routeValues.Count > 0)
                {
                    object controller;
                    routeValues.TryGetValue("controller", out controller);
                    string controllerString = (controller == null) ? string.Empty : controller.ToString();

                    if (!string.IsNullOrEmpty(controllerString))
                    {
                        object action;
                        routeValues.TryGetValue("action", out action);
                        string actionString = (action == null) ? string.Empty : action.ToString();

                        name = controllerString;
                        if (!string.IsNullOrEmpty(actionString))
                        {
                            name += "/" + actionString;
                        }
                        else
                        {
                            if (routeValues.Keys.Count > 1)
                            {
                                // We want to include arguments because in WebApi action is usually null 
                                // and action is resolved by controller, http method and number of arguments
                                var sortedKeys = routeValues.Keys
                                    .Where(key => !string.Equals(key, "controller", StringComparison.OrdinalIgnoreCase))
                                    .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
                                    .ToArray();

                                string arguments = string.Join(@"/", sortedKeys);
                                name += " [" + arguments + "]";
                            }
                        }
                    }
                }
            }

            if (name.StartsWith("/__browserLink/requestData/", StringComparison.OrdinalIgnoreCase))
            {
                name = "/__browserLink";
            }

            name = request.HttpMethod + " " + name;

            return name;
        }

        private static bool TryParseStandardHeaders(
            RequestTelemetry requestTelemetry,
            HttpRequest request)
        {
            var parentId = request.UnvalidatedGetHeader(RequestResponseHeaders.RequestIdHeader);

            // don't bother parsing correlation-context if there was no RequestId
            if (!string.IsNullOrEmpty(parentId))
            {
                var correlationContext =
                    request.Headers.GetNameValueCollectionFromHeader(RequestResponseHeaders.CorrelationContextHeader);

                bool correlationContextHasId = false;
                if (correlationContext != null)
                {
                    foreach (var item in correlationContext)
                    {
                        if (!string.IsNullOrEmpty(item.Key) &&
                            !string.IsNullOrEmpty(item.Value) &&
                            item.Key.Length <= 16 &&
                            item.Value.Length < 42)
                        {
                            if (item.Key == "Id")
                            {
                                correlationContextHasId = true;
                                requestTelemetry.Context.Operation.Id = item.Value;
                                requestTelemetry.Id = AppInsightsActivity.GenerateRequestId(item.Value);
                            }

                            if (!requestTelemetry.Context.Properties.ContainsKey(item.Key))
                            {
                                requestTelemetry.Context.Properties.Add(item);
                            }
                            // requestTelemetry.Context.CorrelationContext[item.Key] = item.Value;
                        }
                    }
                }

                requestTelemetry.Context.Operation.ParentId = parentId;
                if (!correlationContextHasId && AppInsightsActivity.IsHierarchicalRequestId(parentId))
                {
                    requestTelemetry.Context.Operation.Id = AppInsightsActivity.GetRootId(parentId);
                    requestTelemetry.Id = AppInsightsActivity.GenerateRequestId(parentId);
                }

                if (string.IsNullOrEmpty(requestTelemetry.Context.Operation.Id))
                {
                    // ok, upstream gave us non-hirarchical id and no Id in the correlation context
                    // We'll use parentId to generate our Ids.
                    requestTelemetry.Context.Operation.Id = AppInsightsActivity.GetRootId(parentId);
                    requestTelemetry.Id = AppInsightsActivity.GenerateRequestId(parentId);
                }

                return true;
            }

            return false;
        }
    }
}