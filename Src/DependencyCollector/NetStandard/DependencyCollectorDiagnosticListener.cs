// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ApplicationInsights.DependencyCollector
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net.Http;
    using System.Reflection;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;

    internal class DependencyCollectorDiagnosticListener : IObserver<KeyValuePair<string, object>>
    {
        /// <summary>
        /// Source instrumentation header that is added by an application while making http requests and retrieved by the other application when processing incoming requests.
        /// </summary>
        internal const string SourceInstrumentationKeyHeader = "x-ms-request-source-ikey";

        /// <summary>
        /// Target instrumentation header that is added to the response and retrieved by the calling application when processing incoming responses.
        /// </summary>
        internal const string TargetInstrumentationKeyHeader = "x-ms-request-target-ikey";

        /// <summary>
        /// Standard parent Id header.
        /// </summary>
        internal const string StandardParentIdHeader = "x-ms-request-id";

        /// <summary>
        /// Standard root id header.
        /// </summary>
        internal const string StandardRootIdHeader = "x-ms-request-root-id";

        private readonly TelemetryClient client;
        private readonly ConcurrentDictionary<Guid, DependencyTelemetry> requestTelemetry = new ConcurrentDictionary<Guid, DependencyTelemetry>();

        public DependencyCollectorDiagnosticListener()
            : this(new TelemetryClient(TelemetryConfiguration.Active))
        {
        }

        /// <inheritdoc/>
        public DependencyCollectorDiagnosticListener(TelemetryClient client)
        {
            this.client = client;
        }

        private static HttpRequestMessage GetRequest(object value)
        {
            // From https://github.com/dotnet/corefx/blob/bffef76f6af208e2042a2f27bc081ee908bb390b/src/Common/src/System/Net/Http/HttpHandlerDiagnosticListenerExtensions.cs#L60
            PropertyInfo requestInfo = value.GetType().GetRuntimeProperty("Request");
            return (HttpRequestMessage)requestInfo?.GetValue(value, null);
        }

        private static HttpResponseMessage GetResponse(object value)
        {
            // From https://github.com/dotnet/corefx/blob/bffef76f6af208e2042a2f27bc081ee908bb390b/src/Common/src/System/Net/Http/HttpHandlerDiagnosticListenerExtensions.cs#L81
            PropertyInfo requestInfo = value.GetType().GetRuntimeProperty("Response");
            return (HttpResponseMessage)requestInfo?.GetValue(value, null);
        }

        private static Guid? GetLoggingRequestId(object value)
        {
            // From https://github.com/dotnet/corefx/blob/bffef76f6af208e2042a2f27bc081ee908bb390b/src/Common/src/System/Net/Http/HttpHandlerDiagnosticListenerExtensions.cs#L61
            PropertyInfo loggingRequestIdInfo = value.GetType().GetRuntimeProperty("LoggingRequestId");
            return (Guid?)loggingRequestIdInfo?.GetValue(value, null);
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            if (value.Value == null)
                return;

            if (value.Key == "System.Net.Http.Request")
            {
                HttpRequestMessage request = GetRequest(value.Value);
                Guid? loggingRequestId = GetLoggingRequestId(value.Value);

                if (request != null && loggingRequestId != null)
                {
                    string httpMethod = request.Method.Method;
                    Uri requestUri = request.RequestUri;
                    string resourceName = requestUri.AbsolutePath;
                    if (!string.IsNullOrEmpty(httpMethod))
                    {
                        resourceName = httpMethod + " " + resourceName;
                    }

                    DependencyTelemetry telemetry = new DependencyTelemetry();
                    telemetry.Start();
                    this.client.Initialize(telemetry);

                    telemetry.Name = resourceName;
                    telemetry.Target = requestUri.Host;
                    telemetry.Type = "Http";
                    telemetry.Data = requestUri.OriginalString;

                    // Add the source instrumentation key header if collection is enabled, the request host is not in the excluded list and the same header doesn't already exist
                    if (!string.IsNullOrEmpty(telemetry.Context.InstrumentationKey) && !request.Headers.Contains(SourceInstrumentationKeyHeader))
                    {
                        request.Headers.Add(SourceInstrumentationKeyHeader, InstrumentationKeyHashLookupHelper.GetInstrumentationKeyHash(telemetry.Context.InstrumentationKey));
                    }

                    // Add the root ID
                    string rootId = telemetry.Context.Operation.Id;
                    if (!string.IsNullOrEmpty(rootId) && !request.Headers.Contains(StandardRootIdHeader))
                    {
                        request.Headers.Add(StandardRootIdHeader, rootId);
                    }

                    // Add the parent ID
                    string parentId = telemetry.Id;
                    if (!string.IsNullOrEmpty(parentId) && !request.Headers.Contains(StandardParentIdHeader))
                    {
                        request.Headers.Add(StandardParentIdHeader, parentId);
                    }

                    this.requestTelemetry.TryAdd(loggingRequestId.Value, telemetry);
                }
            }
            else if (value.Key == "System.Net.Http.Response")
            {
                HttpResponseMessage response = GetResponse(value.Value);
                Guid? loggingRequestId = GetLoggingRequestId(value.Value);
                if (response != null && loggingRequestId != null)
                {
                    DependencyTelemetry telemetry;
                    if (this.requestTelemetry.TryRemove(loggingRequestId.Value, out telemetry))
                    {
                        if (response.Headers.Contains(TargetInstrumentationKeyHeader))
                        {
                            string targetInstrumentationKeyHash = response.Headers.GetValues(TargetInstrumentationKeyHeader).SingleOrDefault();

                            // We only add the cross component correlation key if the key does not remain the current component.
                            if (!string.IsNullOrEmpty(targetInstrumentationKeyHash) && targetInstrumentationKeyHash != InstrumentationKeyHashLookupHelper.GetInstrumentationKeyHash(telemetry.Context.InstrumentationKey))
                            {
                                telemetry.Type = "Application Insights";
                                telemetry.Target += " | " + targetInstrumentationKeyHash;
                            }
                        }

                        int statusCode = (int)response.StatusCode;
                        telemetry.ResultCode = (0 < statusCode) ? statusCode.ToString(CultureInfo.InvariantCulture) : string.Empty;
                        telemetry.Success = (0 < statusCode) && (statusCode < 400);

                        telemetry.Stop();
                        this.client.Track(telemetry);
                    }
                }
            }
        }

        public void OnCompleted() { }

        public void OnError(Exception error) { }
    }
}