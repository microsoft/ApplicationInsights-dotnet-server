// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ApplicationInsights.DependencyCollector
{
    using Channel;
    using DataContracts;
    using Extensibility;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;

    [TestClass]
    public class DependencyCollectorDiagnosticListenerTests
    {
        private string instrumentationKey;
        private StubTelemetryChannel telemetryChannel;
        private List<ITelemetry> sentTelemetry = new List<ITelemetry>();
        private DependencyCollectorDiagnosticListener listener;

        [TestInitialize]
        public void Initialize()
        {
            instrumentationKey = Guid.NewGuid().ToString();

            telemetryChannel = new StubTelemetryChannel()
            {
                EndpointAddress = "https://endpointaddress",
                OnSend = sentTelemetry.Add
            };

            TelemetryClient client = new TelemetryClient(
                    new TelemetryConfiguration()
                    {
                        TelemetryChannel = telemetryChannel,
                        InstrumentationKey = instrumentationKey,
                    });
            // You'd think that the constructor would set the instrumentation key is the
            // configuration had a instrumentation key, but it doesn't, so we have to set it here.
            client.InstrumentationKey = instrumentationKey;

            listener = new DependencyCollectorDiagnosticListener(client);
        }

        [TestMethod]
        public void OnRequestWithRequestEventWithNoRequestUri()
        {
            listener.OnRequest(new HttpRequestMessage(), Guid.NewGuid());
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);
        }

        [TestMethod]
        public void OnRequestWithRequestEvent()
        {
            Guid loggingRequestId = Guid.NewGuid();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://www.example.com");
            listener.OnRequest(request, loggingRequestId);

            Assert.AreEqual(1, listener.PendingDependencyTelemetry.Count());
            DependencyTelemetry telemetry = listener.PendingDependencyTelemetry.Single();
            Assert.AreEqual("POST /", telemetry.Name);
            Assert.AreEqual("www.example.com", telemetry.Target);
            Assert.AreEqual("Http", telemetry.Type);
            Assert.AreEqual("https://www.example.com", telemetry.Data);
            Assert.AreEqual("", telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);

            Assert.AreEqual(InstrumentationKeyHashLookupHelper.GetInstrumentationKeyHash(instrumentationKey), GetRequestHeaderValues(request, DependencyCollectorDiagnosticListener.SourceInstrumentationKeyHeader).SingleOrDefault());
            Assert.IsFalse(GetRequestHeaderValues(request, DependencyCollectorDiagnosticListener.StandardRootIdHeader).Any());
            Assert.IsTrue(GetRequestHeaderValues(request, DependencyCollectorDiagnosticListener.StandardParentIdHeader).Any());

            Assert.AreEqual(0, sentTelemetry.Count);
        }

        private static IEnumerable<string> GetRequestHeaderValues(HttpRequestMessage request, string headerName)
        {
            return request != null && request.Headers != null && request.Headers.Contains(headerName) ? request.Headers.GetValues(headerName) : Enumerable.Empty<string>();
        }

        [TestMethod]
        public void OnResponseWithResponseEventButNoMatchingRequest()
        {
            listener.OnResponse(new HttpResponseMessage(), Guid.NewGuid());
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);
        }

        [TestMethod]
        public void OnResponseWithSuccessfulResponseEventWithMatchingRequestAndNoTargetInstrumentationKeyHasHeader()
        {
            Guid loggingRequestId = Guid.NewGuid();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://www.example.com");
            listener.OnRequest(request, loggingRequestId);
            Assert.AreEqual(1, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);

            DependencyTelemetry telemetry = listener.PendingDependencyTelemetry.Single();
            Assert.AreEqual("", telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            listener.OnResponse(response, loggingRequestId);
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(1, sentTelemetry.Count);
            Assert.AreSame(telemetry, sentTelemetry.Single());

            Assert.AreEqual("Http", telemetry.Type);
            Assert.AreEqual("www.example.com", telemetry.Target);
            Assert.AreEqual("200", telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);
        }

        [TestMethod]
        public void OnResponseWithFailedResponseEventWithMatchingRequestAndNoTargetInstrumentationKeyHasHeader()
        {
            Guid loggingRequestId = Guid.NewGuid();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://www.example.com");
            listener.OnRequest(request, loggingRequestId);
            Assert.AreEqual(1, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);

            DependencyTelemetry telemetry = listener.PendingDependencyTelemetry.Single();
            Assert.AreEqual("", telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.NotFound);
            listener.OnResponse(response, loggingRequestId);
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(1, sentTelemetry.Count);
            Assert.AreSame(telemetry, sentTelemetry.Single());

            Assert.AreEqual("Http", telemetry.Type);
            Assert.AreEqual("www.example.com", telemetry.Target);
            Assert.AreEqual("404", telemetry.ResultCode);
            Assert.AreEqual(false, telemetry.Success);
        }

        [TestMethod]
        public void OnResponseWithSuccessfulResponseEventWithMatchingRequestAndSameTargetInstrumentationKeyHashHeader()
        {
            Guid loggingRequestId = Guid.NewGuid();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://www.example.com");
            listener.OnRequest(request, loggingRequestId);
            Assert.AreEqual(1, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);

            DependencyTelemetry telemetry = listener.PendingDependencyTelemetry.Single();
            Assert.AreEqual("", telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            string targetInstrumentationKeyHash = InstrumentationKeyHashLookupHelper.GetInstrumentationKeyHash(instrumentationKey);
            response.Headers.Add(DependencyCollectorDiagnosticListener.TargetInstrumentationKeyHeader, targetInstrumentationKeyHash);

            listener.OnResponse(response, loggingRequestId);
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(1, sentTelemetry.Count);
            Assert.AreSame(telemetry, sentTelemetry.Single());

            Assert.AreEqual("Http", telemetry.Type);
            Assert.AreEqual("www.example.com", telemetry.Target);
            Assert.AreEqual("200", telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);
        }

        [TestMethod]
        public void OnResponseWithFailedResponseEventWithMatchingRequestAndSameTargetInstrumentationKeyHashHeader()
        {
            Guid loggingRequestId = Guid.NewGuid();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://www.example.com");
            listener.OnRequest(request, loggingRequestId);
            Assert.AreEqual(1, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);

            DependencyTelemetry telemetry = listener.PendingDependencyTelemetry.Single();
            Assert.AreEqual("", telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.NotFound);
            string targetInstrumentationKeyHash = InstrumentationKeyHashLookupHelper.GetInstrumentationKeyHash(instrumentationKey);
            response.Headers.Add(DependencyCollectorDiagnosticListener.TargetInstrumentationKeyHeader, targetInstrumentationKeyHash);

            listener.OnResponse(response, loggingRequestId);
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(1, sentTelemetry.Count);
            Assert.AreSame(telemetry, sentTelemetry.Single());

            Assert.AreEqual("Http", telemetry.Type);
            Assert.AreEqual("www.example.com", telemetry.Target);
            Assert.AreEqual("404", telemetry.ResultCode);
            Assert.AreEqual(false, telemetry.Success);
        }

        [TestMethod]
        public void OnResponseWithSuccessfulResponseEventWithMatchingRequestAndDifferentTargetInstrumentationKeyHashHeader()
        {
            Guid loggingRequestId = Guid.NewGuid();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://www.example.com");
            listener.OnRequest(request, loggingRequestId);
            Assert.AreEqual(1, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);

            DependencyTelemetry telemetry = listener.PendingDependencyTelemetry.Single();
            Assert.AreEqual("", telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            string targetInstrumentationKeyHash = InstrumentationKeyHashLookupHelper.GetInstrumentationKeyHash(Guid.NewGuid().ToString());
            response.Headers.Add(DependencyCollectorDiagnosticListener.TargetInstrumentationKeyHeader, targetInstrumentationKeyHash);

            listener.OnResponse(response, loggingRequestId);
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(1, sentTelemetry.Count);
            Assert.AreSame(telemetry, sentTelemetry.Single());

            Assert.AreEqual("Application Insights", telemetry.Type);
            Assert.AreEqual($"www.example.com | {targetInstrumentationKeyHash}", telemetry.Target);
            Assert.AreEqual("200", telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);
        }

        [TestMethod]
        public void OnResponseWithFailedResponseEventWithMatchingRequestAndDifferentTargetInstrumentationKeyHashHeader()
        {
            Guid loggingRequestId = Guid.NewGuid();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://www.example.com");
            listener.OnRequest(request, loggingRequestId);
            Assert.AreEqual(1, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);

            DependencyTelemetry telemetry = listener.PendingDependencyTelemetry.Single();
            Assert.AreEqual("", telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.NotFound);
            string targetInstrumentationKeyHash = InstrumentationKeyHashLookupHelper.GetInstrumentationKeyHash(Guid.NewGuid().ToString());
            response.Headers.Add(DependencyCollectorDiagnosticListener.TargetInstrumentationKeyHeader, targetInstrumentationKeyHash);

            listener.OnResponse(response, loggingRequestId);
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(1, sentTelemetry.Count);
            Assert.AreSame(telemetry, sentTelemetry.Single());

            Assert.AreEqual("Application Insights", telemetry.Type);
            Assert.AreEqual($"www.example.com | {targetInstrumentationKeyHash}", telemetry.Target);
            Assert.AreEqual("404", telemetry.ResultCode);
            Assert.AreEqual(false, telemetry.Success);
        }
    }
}
