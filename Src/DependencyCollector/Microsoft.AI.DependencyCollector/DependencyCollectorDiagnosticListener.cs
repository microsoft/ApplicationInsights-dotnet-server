// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ApplicationInsights.DependencyCollector
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Net.Http;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.Extensions.DiagnosticAdapter;

    public static class DependencyCollectorExtensions
    {
        /// <summary>
        /// Adds Application Insights Dependency Collector services into service collection.
        /// </summary>
        /// <param name="services">The <see cref="IServiceCollection"/> insance.</param>
        /// <param name="options">The action used to configure the options.</param>
        /// <returns>
        /// The <see cref="IServiceCollection"/>.
        /// </returns>
        public static void AddApplicationInsightsDependencyCollector(this IObservable<DiagnosticListener> diagnosticListeners)
        {
            diagnosticListeners.Subscribe(new DependencyCollectorInitializer(new DependencyCollectorDiagnosticListener()));
        }
    }

    /// <summary>
    /// Class used to initialize Application Insights Dependency Collector diagnostic listeners.
    /// </summary>
    internal class DependencyCollectorInitializer : IObserver<DiagnosticListener>, IDisposable
    {
        private readonly List<IDisposable> subscriptions;
        private readonly DependencyCollectorDiagnosticListener diagnosticListener;

        internal DependencyCollectorInitializer(DependencyCollectorDiagnosticListener diagnosticListener)
        {
            this.subscriptions = new List<IDisposable>();
            this.diagnosticListener = diagnosticListener;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (IDisposable subscription in this.subscriptions)
                {
                    subscription.Dispose();
                }
            }
        }

        public void OnNext(DiagnosticListener value)
        {
            if (diagnosticListener.ListenerName == value.Name)
            {
                this.subscriptions.Add(value.SubscribeWithAdapter(diagnosticListener));
            }
        }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }
    }

    /// <summary>
    /// Diagnostic listener implementation that listens for events specific to outgoing depedency requests.
    /// </summary>
    public class DependencyCollectorDiagnosticListener
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
        private readonly ConcurrentDictionary<Guid, DependencyTelemetry> pendingTelemetry = new ConcurrentDictionary<Guid, DependencyTelemetry>();

        public DependencyCollectorDiagnosticListener()
            : this(new TelemetryClient(TelemetryConfiguration.Active))
        {
            this.client = new TelemetryClient(TelemetryConfiguration.Active);
            // You'd think that the constructor would set the instrumentation key is the
            // configuration had a instrumentation key, but it doesn't, so we have to set it here.
            this.client.InstrumentationKey = TelemetryConfiguration.Active.InstrumentationKey;
        }

        public DependencyCollectorDiagnosticListener(TelemetryClient client)
        {
            this.client = client;
        }

        public string ListenerName
        {
            // Comes from https://github.com/dotnet/corefx/blob/master/src/System.Net.Http/src/System/Net/Http/DiagnosticsHandlerLoggingStrings.cs#L12
            get { return "HttpHandlerDiagnosticListener"; }
        }

        /// <summary>
        /// Get the DependencyTelemetry objects that are still waiting for a response from the dependency.
        /// </summary>
        internal IEnumerable<DependencyTelemetry> PendingDependencyTelemetry
        {
            get { return pendingTelemetry.Values; }
        }

        /// <summary>
        /// Diagnostic event handler method for 'System.Net.Http.Request' event.
        /// </summary>
        [DiagnosticName("System.Net.Http.Request")]
        public void OnRequest(HttpRequestMessage request, Guid loggingRequestId)
        {
            Console.WriteLine("DASCHULT - OnRequest() - Enter");
            if (request != null && request.RequestUri != null)
            {
                string httpMethod = request.Method.Method;
                Uri requestUri = request.RequestUri;
                string resourceName = requestUri.AbsolutePath;
                if (!string.IsNullOrEmpty(httpMethod))
                {
                    resourceName = httpMethod + " " + resourceName;
                }

                DependencyTelemetry telemetry = new DependencyTelemetry();
                this.client.Initialize(telemetry);
                telemetry.Start();
                telemetry.Name = resourceName;
                telemetry.Target = requestUri.Host;
                telemetry.Type = "Http";
                telemetry.Data = requestUri.OriginalString;
                this.pendingTelemetry.TryAdd(loggingRequestId, telemetry);

                if (!request.Headers.Contains(SourceInstrumentationKeyHeader))
                {
                    request.Headers.Add(SourceInstrumentationKeyHeader, InstrumentationKeyHashLookupHelper.GetInstrumentationKeyHash(this.client.InstrumentationKey));
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
            }
            Console.WriteLine("DASCHULT - OnRequest() - Exit");
        }

        /// <summary>
        /// Diagnostic event handler method for 'System.Net.Http.Response' event. Even in the case of an exception, this will still be called.
        /// See https://github.com/dotnet/corefx/blob/master/src/System.Net.Http/src/System/Net/Http/DiagnosticsHandler.cs#L71 for more information.
        /// </summary>
        [DiagnosticName("System.Net.Http.Response")]
        public void OnResponse(HttpResponseMessage response, Guid loggingRequestId)
        {
            Console.WriteLine("DASCHULT - OnResponse() - Enter");
            if (response != null)
            {
                DependencyTelemetry telemetry;
                if (this.pendingTelemetry.TryRemove(loggingRequestId, out telemetry))
                {
                    if (response.Headers.Contains(TargetInstrumentationKeyHeader))
                    {
                        string targetInstrumentationKeyHash = response.Headers.GetValues(TargetInstrumentationKeyHeader).SingleOrDefault();

                        // We only add the cross component correlation key if the key does not represent the current component.
                        if (!string.IsNullOrEmpty(targetInstrumentationKeyHash) && targetInstrumentationKeyHash != InstrumentationKeyHashLookupHelper.GetInstrumentationKeyHash(this.client.InstrumentationKey))
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
            Console.WriteLine("DASCHULT - OnResponse() - Exit");
        }
    }
}