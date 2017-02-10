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

            listener = new DependencyCollectorDiagnosticListener(
                new TelemetryClient(
                    new TelemetryConfiguration()
                    {
                        TelemetryChannel = telemetryChannel,
                        InstrumentationKey = instrumentationKey,
                    }));
        }

        [TestMethod]
        public void OnNextWithNullValue()
        {
            listener.OnNext(new KeyValuePair<string, object>("test event name", null));
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);
        }

        [TestMethod]
        public void OnNextWithNullKey()
        {
            listener.OnNext(new KeyValuePair<string, object>(null, "test event value"));
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);
        }

        [TestMethod]
        public void OnNextWithUnrecognizedEvent()
        {
            listener.OnNext(new KeyValuePair<string, object>("test event name", "test event value"));
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);
        }

        [TestMethod]
        public void OnNextWithRequestEventWithNullValue()
        {
            listener.OnNext(new KeyValuePair<string, object>("System.Net.Http.Request", null));
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);
        }

        [TestMethod]
        public void OnNextWithRequestEventWithNoRequestOrLoggingRequestId()
        {
            listener.OnNext(new KeyValuePair<string, object>("System.Net.Http.Request", new { }));
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);
        }

        [TestMethod]
        public void OnNextWithRequestEventWithNoRequest()
        {
            listener.OnNext(new KeyValuePair<string, object>("System.Net.Http.Request", new { LoggingRequestId = Guid.NewGuid() }));
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);
        }

        [TestMethod]
        public void OnNextWithRequestEventWithNoLoggingRequestId()
        {
            listener.OnNext(new KeyValuePair<string, object>("System.Net.Http.Request", new { Request = new HttpRequestMessage() }));
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);
        }

        [TestMethod]
        public void OnNextWithRequestEventWithNoRequestUri()
        {
            listener.OnNext(new KeyValuePair<string, object>("System.Net.Http.Request", new { LoggingRequestId = Guid.NewGuid(), Request = new HttpRequestMessage() }));
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);
        }

        [TestMethod]
        public void OnNextWithRequestEvent()
        {
            Guid loggingRequestId = Guid.NewGuid();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://www.example.com");
            listener.OnNext(new KeyValuePair<string, object>("System.Net.Http.Request", new { LoggingRequestId = loggingRequestId, Request = request }));

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
        public void OnNextWithResponseEventWithNullValue()
        {
            listener.OnNext(new KeyValuePair<string, object>("System.Net.Http.Response", null));
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);
        }

        [TestMethod]
        public void OnNextWithResponseEventWithNoResponseOrLoggingRequestId()
        {
            listener.OnNext(new KeyValuePair<string, object>("System.Net.Http.Response", new { }));
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);
        }

        [TestMethod]
        public void OnNextWithResponseEventWithNoLoggingRequestId()
        {
            listener.OnNext(new KeyValuePair<string, object>("System.Net.Http.Response", new { Response = new HttpResponseMessage() }));
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);
        }

        [TestMethod]
        public void OnNextWithResponseEventWithNoResponse()
        {
            listener.OnNext(new KeyValuePair<string, object>("System.Net.Http.Response", new { LoggingRequestId = Guid.NewGuid() }));
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);
        }

        [TestMethod]
        public void OnNextWithResponseEventButNoMatchingRequest()
        {
            listener.OnNext(new KeyValuePair<string, object>("System.Net.Http.Response", new { LoggingRequestId = Guid.NewGuid(), Response = new HttpResponseMessage() }));
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);
        }

        [TestMethod]
        public void OnNextWithSuccessfulResponseEventWithMatchingRequestAndNoTargetInstrumentationKeyHasHeader()
        {
            Guid loggingRequestId = Guid.NewGuid();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://www.example.com");
            listener.OnNext(new KeyValuePair<string, object>("System.Net.Http.Request", new { LoggingRequestId = loggingRequestId, Request = request }));
            Assert.AreEqual(1, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);

            DependencyTelemetry telemetry = listener.PendingDependencyTelemetry.Single();
            Assert.AreEqual("", telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            listener.OnNext(new KeyValuePair<string, object>("System.Net.Http.Response", new { LoggingRequestId = loggingRequestId, Response = response }));
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(1, sentTelemetry.Count);
            Assert.AreSame(telemetry, sentTelemetry.Single());

            Assert.AreEqual("Http", telemetry.Type);
            Assert.AreEqual("www.example.com", telemetry.Target);
            Assert.AreEqual("200", telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);
        }

        [TestMethod]
        public void OnNextWithFailedResponseEventWithMatchingRequestAndNoTargetInstrumentationKeyHasHeader()
        {
            Guid loggingRequestId = Guid.NewGuid();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://www.example.com");
            listener.OnNext(new KeyValuePair<string, object>("System.Net.Http.Request", new { LoggingRequestId = loggingRequestId, Request = request }));
            Assert.AreEqual(1, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);

            DependencyTelemetry telemetry = listener.PendingDependencyTelemetry.Single();
            Assert.AreEqual("", telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.NotFound);
            listener.OnNext(new KeyValuePair<string, object>("System.Net.Http.Response", new { LoggingRequestId = loggingRequestId, Response = response }));
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(1, sentTelemetry.Count);
            Assert.AreSame(telemetry, sentTelemetry.Single());

            Assert.AreEqual("Http", telemetry.Type);
            Assert.AreEqual("www.example.com", telemetry.Target);
            Assert.AreEqual("404", telemetry.ResultCode);
            Assert.AreEqual(false, telemetry.Success);
        }

        [TestMethod]
        public void OnNextWithSuccessfulResponseEventWithMatchingRequestAndSameTargetInstrumentationKeyHashHeader()
        {
            Guid loggingRequestId = Guid.NewGuid();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://www.example.com");
            listener.OnNext(new KeyValuePair<string, object>("System.Net.Http.Request", new { LoggingRequestId = loggingRequestId, Request = request }));
            Assert.AreEqual(1, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);

            DependencyTelemetry telemetry = listener.PendingDependencyTelemetry.Single();
            Assert.AreEqual("", telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            string targetInstrumentationKeyHash = InstrumentationKeyHashLookupHelper.GetInstrumentationKeyHash(instrumentationKey);
            response.Headers.Add(DependencyCollectorDiagnosticListener.TargetInstrumentationKeyHeader, targetInstrumentationKeyHash);

            listener.OnNext(new KeyValuePair<string, object>("System.Net.Http.Response", new { LoggingRequestId = loggingRequestId, Response = response }));
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(1, sentTelemetry.Count);
            Assert.AreSame(telemetry, sentTelemetry.Single());

            Assert.AreEqual("Http", telemetry.Type);
            Assert.AreEqual("www.example.com", telemetry.Target);
            Assert.AreEqual("200", telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);
        }

        [TestMethod]
        public void OnNextWithFailedResponseEventWithMatchingRequestAndSameTargetInstrumentationKeyHashHeader()
        {
            Guid loggingRequestId = Guid.NewGuid();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://www.example.com");
            listener.OnNext(new KeyValuePair<string, object>("System.Net.Http.Request", new { LoggingRequestId = loggingRequestId, Request = request }));
            Assert.AreEqual(1, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);

            DependencyTelemetry telemetry = listener.PendingDependencyTelemetry.Single();
            Assert.AreEqual("", telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.NotFound);
            string targetInstrumentationKeyHash = InstrumentationKeyHashLookupHelper.GetInstrumentationKeyHash(instrumentationKey);
            response.Headers.Add(DependencyCollectorDiagnosticListener.TargetInstrumentationKeyHeader, targetInstrumentationKeyHash);

            listener.OnNext(new KeyValuePair<string, object>("System.Net.Http.Response", new { LoggingRequestId = loggingRequestId, Response = response }));
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(1, sentTelemetry.Count);
            Assert.AreSame(telemetry, sentTelemetry.Single());

            Assert.AreEqual("Http", telemetry.Type);
            Assert.AreEqual("www.example.com", telemetry.Target);
            Assert.AreEqual("404", telemetry.ResultCode);
            Assert.AreEqual(false, telemetry.Success);
        }

        [TestMethod]
        public void OnNextWithSuccessfulResponseEventWithMatchingRequestAndDifferentTargetInstrumentationKeyHashHeader()
        {
            Guid loggingRequestId = Guid.NewGuid();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://www.example.com");
            listener.OnNext(new KeyValuePair<string, object>("System.Net.Http.Request", new { LoggingRequestId = loggingRequestId, Request = request }));
            Assert.AreEqual(1, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);

            DependencyTelemetry telemetry = listener.PendingDependencyTelemetry.Single();
            Assert.AreEqual("", telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.OK);
            string targetInstrumentationKeyHash = InstrumentationKeyHashLookupHelper.GetInstrumentationKeyHash(Guid.NewGuid().ToString());
            response.Headers.Add(DependencyCollectorDiagnosticListener.TargetInstrumentationKeyHeader, targetInstrumentationKeyHash);

            listener.OnNext(new KeyValuePair<string, object>("System.Net.Http.Response", new { LoggingRequestId = loggingRequestId, Response = response }));
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(1, sentTelemetry.Count);
            Assert.AreSame(telemetry, sentTelemetry.Single());

            Assert.AreEqual("Application Insights", telemetry.Type);
            Assert.AreEqual($"www.example.com | {targetInstrumentationKeyHash}", telemetry.Target);
            Assert.AreEqual("200", telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);
        }

        [TestMethod]
        public void OnNextWithFailedResponseEventWithMatchingRequestAndDifferentTargetInstrumentationKeyHashHeader()
        {
            Guid loggingRequestId = Guid.NewGuid();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "https://www.example.com");
            listener.OnNext(new KeyValuePair<string, object>("System.Net.Http.Request", new { LoggingRequestId = loggingRequestId, Request = request }));
            Assert.AreEqual(1, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);

            DependencyTelemetry telemetry = listener.PendingDependencyTelemetry.Single();
            Assert.AreEqual("", telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);

            HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.NotFound);
            string targetInstrumentationKeyHash = InstrumentationKeyHashLookupHelper.GetInstrumentationKeyHash(Guid.NewGuid().ToString());
            response.Headers.Add(DependencyCollectorDiagnosticListener.TargetInstrumentationKeyHeader, targetInstrumentationKeyHash);

            listener.OnNext(new KeyValuePair<string, object>("System.Net.Http.Response", new { LoggingRequestId = loggingRequestId, Response = response }));
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
