namespace Microsoft.ApplicationInsights.DependencyCollector
{
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;

    [TestClass]
    public class DependencyCollectorDiagnosticListenerTests
    {
        private const string requestUrl = "www.example.com";
        private const string requestUrlWithScheme = "https://" + requestUrl;
        private const string httpType = "Http";
        private const string applicationInsightsType = "Application Insights";
        private const string okResultCode = "200";
        private const string notFoundResultCode = "404";

        private static string GetApplicationInsightsTarget(string targetInstrumentationKeyHash)
        {
            return $"{requestUrl} | {targetInstrumentationKeyHash}";
        }

        private string instrumentationKey;
        private StubTelemetryChannel telemetryChannel;
        private DependencyCollectorDiagnosticListener listener;

        private List<ITelemetry> sentTelemetry = new List<ITelemetry>();

        [TestInitialize]
        public void Initialize()
        {
            instrumentationKey = Guid.NewGuid().ToString();

            telemetryChannel = new StubTelemetryChannel()
            {
                EndpointAddress = "https://endpointaddress",
                OnSend = sentTelemetry.Add
            };

            listener = new DependencyCollectorDiagnosticListener(new TelemetryConfiguration()
            {
                TelemetryChannel = telemetryChannel,
                InstrumentationKey = instrumentationKey,
            });
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
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrlWithScheme);
            listener.OnRequest(request, loggingRequestId);

            Assert.AreEqual(1, listener.PendingDependencyTelemetry.Count());
            DependencyTelemetry telemetry = listener.PendingDependencyTelemetry.Single();
            Assert.AreEqual("POST /", telemetry.Name);
            Assert.AreEqual(requestUrl, telemetry.Target);
            Assert.AreEqual(httpType, telemetry.Type);
            Assert.AreEqual(requestUrlWithScheme, telemetry.Data);
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
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrlWithScheme);
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

            Assert.AreEqual(httpType, telemetry.Type);
            Assert.AreEqual(requestUrl, telemetry.Target);
            Assert.AreEqual(okResultCode, telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);
        }

        [TestMethod]
        public void OnResponseWithFailedResponseEventWithMatchingRequestAndNoTargetInstrumentationKeyHasHeader()
        {
            Guid loggingRequestId = Guid.NewGuid();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrlWithScheme);
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

            Assert.AreEqual(httpType, telemetry.Type);
            Assert.AreEqual(requestUrl, telemetry.Target);
            Assert.AreEqual(notFoundResultCode, telemetry.ResultCode);
            Assert.AreEqual(false, telemetry.Success);
        }

        [TestMethod]
        public void OnResponseWithSuccessfulResponseEventWithMatchingRequestAndSameTargetInstrumentationKeyHashHeader()
        {
            Guid loggingRequestId = Guid.NewGuid();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrlWithScheme);
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

            Assert.AreEqual(httpType, telemetry.Type);
            Assert.AreEqual(requestUrl, telemetry.Target);
            Assert.AreEqual(okResultCode, telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);
        }

        [TestMethod]
        public void OnResponseWithFailedResponseEventWithMatchingRequestAndSameTargetInstrumentationKeyHashHeader()
        {
            Guid loggingRequestId = Guid.NewGuid();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrlWithScheme);
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

            Assert.AreEqual(httpType, telemetry.Type);
            Assert.AreEqual(requestUrl, telemetry.Target);
            Assert.AreEqual(notFoundResultCode, telemetry.ResultCode);
            Assert.AreEqual(false, telemetry.Success);
        }

        [TestMethod]
        public void OnResponseWithSuccessfulResponseEventWithMatchingRequestAndDifferentTargetInstrumentationKeyHashHeader()
        {
            Guid loggingRequestId = Guid.NewGuid();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrlWithScheme);
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

            Assert.AreEqual(applicationInsightsType, telemetry.Type);
            Assert.AreEqual(GetApplicationInsightsTarget(targetInstrumentationKeyHash), telemetry.Target);
            Assert.AreEqual(okResultCode, telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);
        }

        [TestMethod]
        public void OnResponseWithFailedResponseEventWithMatchingRequestAndDifferentTargetInstrumentationKeyHashHeader()
        {
            Guid loggingRequestId = Guid.NewGuid();
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUrlWithScheme);
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

            Assert.AreEqual(applicationInsightsType, telemetry.Type);
            Assert.AreEqual(GetApplicationInsightsTarget(targetInstrumentationKeyHash), telemetry.Target);
            Assert.AreEqual(notFoundResultCode, telemetry.ResultCode);
            Assert.AreEqual(false, telemetry.Success);
        }
    }
}
