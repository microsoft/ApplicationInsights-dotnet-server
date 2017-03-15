namespace Microsoft.ApplicationInsights.DependencyCollector
{
    using Implementation;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.Common;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;

    /// <summary>
    /// Unit tests for DependencyCollectorDiagnosticListener.
    /// </summary>
    [TestClass]
    public class DependencyCollectorDiagnosticListenerTests
    {
        private const string requestUrl = "www.example.com";
        private const string requestUrlWithScheme = "https://" + requestUrl;
        private const string okResultCode = "200";
        private const string notFoundResultCode = "404";
        private const string mockAppId = "MOCK_APP_ID";
        private const string mockAppId2 = "MOCK_APP_ID_2";

        private static string GetApplicationInsightsTarget(string targetApplicationId)
        {
            return $"{requestUrl} | {targetApplicationId}";
        }

        private string instrumentationKey;
        private StubTelemetryChannel telemetryChannel;
        private MockCorrelationIdLookupHelper mockCorrelationIdLookupHelper;
        private DependencyCollectorDiagnosticListener listener;

        private List<ITelemetry> sentTelemetry = new List<ITelemetry>();

        /// <summary>
        /// Initialize function that gets called once before any tests in this class are run.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            instrumentationKey = Guid.NewGuid().ToString();

            telemetryChannel = new StubTelemetryChannel()
            {
                EndpointAddress = "https://endpointaddress",
                OnSend = sentTelemetry.Add
            };

            mockCorrelationIdLookupHelper = new MockCorrelationIdLookupHelper(new Dictionary<string, string>()
            {
                [instrumentationKey] = mockAppId
            });

            listener = new DependencyCollectorDiagnosticListener(new TelemetryConfiguration()
            {
                TelemetryChannel = telemetryChannel,
                InstrumentationKey = instrumentationKey,
            },
            mockCorrelationIdLookupHelper);
        }

        /// <summary>
        /// Call OnRequest() with no uri in the HttpRequestMessage.
        /// </summary>
        [TestMethod]
        public void OnRequestWithRequestEventWithNoRequestUri()
        {
            listener.OnRequest(new HttpRequestMessage(), Guid.NewGuid());
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);
        }

        /// <summary>
        /// Call OnRequest() with valid arguments.
        /// </summary>
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
            Assert.AreEqual(RemoteDependencyConstants.HTTP, telemetry.Type);
            Assert.AreEqual(requestUrlWithScheme, telemetry.Data);
            Assert.AreEqual("", telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);

            Assert.AreEqual(mockAppId, GetRequestContextKeyValue(request, RequestResponseHeaders.RequestContextSourceKey));
            Assert.AreEqual(null, GetRequestContextKeyValue(request, RequestResponseHeaders.StandardRootIdHeader));
            Assert.IsFalse(string.IsNullOrEmpty(GetRequestHeaderValues(request, RequestResponseHeaders.StandardParentIdHeader).SingleOrDefault()));

            Assert.AreEqual(0, sentTelemetry.Count);
        }

        private static IEnumerable<string> GetRequestHeaderValues(HttpRequestMessage request, string headerName)
        {
            return DependencyCollectorDiagnosticListener.GetHeaderValues(request.Headers, headerName);
        }

        private static string GetRequestContextKeyValue(HttpRequestMessage request, string keyName)
        {
            return DependencyCollectorDiagnosticListener.GetRequestContextKeyValue(request.Headers, keyName);
        }

        /// <summary>
        /// Call OnResponse() when no matching OnRequest() call has been made.
        /// </summary>
        [TestMethod]
        public void OnResponseWithResponseEventButNoMatchingRequest()
        {
            listener.OnResponse(new HttpResponseMessage(), Guid.NewGuid());
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(0, sentTelemetry.Count);
        }

        /// <summary>
        /// Call OnResponse() with a successful request but no target instrumentation key in the response headers.
        /// </summary>
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

            Assert.AreEqual(RemoteDependencyConstants.HTTP, telemetry.Type);
            Assert.AreEqual(requestUrl, telemetry.Target);
            Assert.AreEqual(okResultCode, telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);
        }

        /// <summary>
        /// Call OnResponse() with a not found request result but no target instrumentation key in the response headers.
        /// </summary>
        [TestMethod]
        public void OnResponseWithNotFoundResponseEventWithMatchingRequestAndNoTargetInstrumentationKeyHasHeader()
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

            Assert.AreEqual(RemoteDependencyConstants.HTTP, telemetry.Type);
            Assert.AreEqual(requestUrl, telemetry.Target);
            Assert.AreEqual(notFoundResultCode, telemetry.ResultCode);
            Assert.AreEqual(false, telemetry.Success);
        }

        /// <summary>
        /// Call OnResponse() with a successful request and same target instrumentation key in the response headers as the source instrumentation key.
        /// </summary>
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
            response.Headers.Add(RequestResponseHeaders.RequestContextTargetKey, mockAppId);

            listener.OnResponse(response, loggingRequestId);
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(1, sentTelemetry.Count);
            Assert.AreSame(telemetry, sentTelemetry.Single());

            Assert.AreEqual(RemoteDependencyConstants.HTTP, telemetry.Type);
            Assert.AreEqual(requestUrl, telemetry.Target);
            Assert.AreEqual(okResultCode, telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);
        }

        /// <summary>
        /// Call OnResponse() with a not found request result code and same target instrumentation key in the response headers as the source instrumentation key.
        /// </summary>
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
            response.Headers.Add(RequestResponseHeaders.RequestContextTargetKey, mockAppId);

            listener.OnResponse(response, loggingRequestId);
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(1, sentTelemetry.Count);
            Assert.AreSame(telemetry, sentTelemetry.Single());

            Assert.AreEqual(RemoteDependencyConstants.HTTP, telemetry.Type);
            Assert.AreEqual(requestUrl, telemetry.Target);
            Assert.AreEqual(notFoundResultCode, telemetry.ResultCode);
            Assert.AreEqual(false, telemetry.Success);
        }

        /// <summary>
        /// Call OnResponse() with a successful request and different target instrumentation key in the response headers than the source instrumentation key.
        /// </summary>
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
            string targetApplicationId = mockAppId2;
            DependencyCollectorDiagnosticListener.SetRequestContextKeyValue(response.Headers, RequestResponseHeaders.RequestContextTargetKey, targetApplicationId);

            listener.OnResponse(response, loggingRequestId);
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(1, sentTelemetry.Count);
            Assert.AreSame(telemetry, sentTelemetry.Single());

            Assert.AreEqual(RemoteDependencyConstants.AI, telemetry.Type);
            Assert.AreEqual(GetApplicationInsightsTarget(targetApplicationId), telemetry.Target);
            Assert.AreEqual(okResultCode, telemetry.ResultCode);
            Assert.AreEqual(true, telemetry.Success);
        }

        /// <summary>
        /// Call OnResponse() with a not found request result code and different target instrumentation key in the response headers than the source instrumentation key.
        /// </summary>
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
            string targetApplicationId = mockAppId2;
            DependencyCollectorDiagnosticListener.SetRequestContextKeyValue(response.Headers, RequestResponseHeaders.RequestContextTargetKey, targetApplicationId);

            listener.OnResponse(response, loggingRequestId);
            Assert.AreEqual(0, listener.PendingDependencyTelemetry.Count());
            Assert.AreEqual(1, sentTelemetry.Count);
            Assert.AreSame(telemetry, sentTelemetry.Single());

            Assert.AreEqual(RemoteDependencyConstants.AI, telemetry.Type);
            Assert.AreEqual(GetApplicationInsightsTarget(targetApplicationId), telemetry.Target);
            Assert.AreEqual(notFoundResultCode, telemetry.ResultCode);
            Assert.AreEqual(false, telemetry.Success);
        }

        /// <summary>
        /// Ensure that GetHeaderValues() returns an empty IEnumerable when the headers argument is null.
        /// </summary>
        [TestMethod]
        public void GetHeaderValuesWithNullHeaders()
        {
            EnumerableAssert.AreEqual(Enumerable.Empty<string>(), DependencyCollectorDiagnosticListener.GetHeaderValues(null, "MOCK_HEADER_NAME"));
        }

        /// <summary>
        /// Ensure that GetHeaderValues() returns an empty IEnumerable when the headers argument is empty.
        /// </summary>
        [TestMethod]
        public void GetHeaderValuesWithEmptyHeaders()
        {
            HttpHeaders headers = CreateHeaders();
            EnumerableAssert.AreEqual(Enumerable.Empty<string>(), DependencyCollectorDiagnosticListener.GetHeaderValues(headers, "MOCK_HEADER_NAME"));
        }

        /// <summary>
        /// Ensure that GetHeaderValues() returns an IEnumerable that contains the key value when the headers argument contains the key name.
        /// </summary>
        [TestMethod]
        public void GetHeaderValuesWithMatchingHeader()
        {
            HttpHeaders headers = CreateHeaders();
            headers.Add("MOCK_HEADER_NAME", "MOCK_HEADER_VALUE");
            EnumerableAssert.AreEqual(new[] { "MOCK_HEADER_VALUE" }, DependencyCollectorDiagnosticListener.GetHeaderValues(headers, "MOCK_HEADER_NAME"));
        }

        /// <summary>
        /// Ensure that GetHeaderValues() returns an IEnumerable that contains all of the values when the headers argument contains multiple values for the key name.
        /// </summary>
        [TestMethod]
        public void GetHeaderValuesWithMultipleMatchingHeaders()
        {
            HttpHeaders headers = CreateHeaders();
            headers.Add("MOCK_HEADER_NAME", "A");
            headers.Add("MOCK_HEADER_NAME", "B");
            headers.Add("MOCK_HEADER_NAME", "C");
            EnumerableAssert.AreEqual(new[] { "A", "B", "C" }, DependencyCollectorDiagnosticListener.GetHeaderValues(headers, "MOCK_HEADER_NAME"));
        }

        /// <summary>
        /// Ensure that GetHeaderKeyValue() returns null when the headers argument is null.
        /// </summary>
        [TestMethod]
        public void GetHeaderKeyValuesWithNullHeaders()
        {
            Assert.AreEqual(null, DependencyCollectorDiagnosticListener.GetHeaderKeyValue(null, "HEADER_NAME", "KEY_NAME"));
        }

        /// <summary>
        /// Ensure that GetHeaderKeyValue() returns null when the headers argument is empty.
        /// </summary>
        [TestMethod]
        public void GetHeaderKeyValuesWithEmptyHeaders()
        {
            HttpHeaders headers = CreateHeaders();
            Assert.AreEqual(null, DependencyCollectorDiagnosticListener.GetHeaderKeyValue(headers, "HEADER_NAME", "KEY_NAME"));
        }

        /// <summary>
        /// Ensure that GetHeaderKeyValue() returns key value when the headers argument contains header key name.
        /// </summary>
        [TestMethod]
        public void GetHeaderKeyValuesWithMatchingHeader()
        {
            HttpHeaders headers = CreateHeaders();
            headers.Add("HEADER_NAME", "KEY_NAME=KEY_VALUE");
            Assert.AreEqual("KEY_VALUE", DependencyCollectorDiagnosticListener.GetHeaderKeyValue(headers, "HEADER_NAME", "KEY_NAME"));
        }

        /// <summary>
        /// Ensure that GetHeaderKeyValue() returns first key value when the headers argument contains multiple key name/value pairs for header name.
        /// </summary>
        [TestMethod]
        public void GetHeaderKeyValuesWithMultipleMatchingHeaderNamesButOnlyOneMatchingKeyName()
        {
            HttpHeaders headers = CreateHeaders();
            headers.Add("HEADER_NAME", "A=a");
            headers.Add("HEADER_NAME", "B=b");
            headers.Add("HEADER_NAME", "C=c");
            Assert.AreEqual("b", DependencyCollectorDiagnosticListener.GetHeaderKeyValue(headers, "HEADER_NAME", "B"));
        }

        /// <summary>
        /// Ensure that GetHeaderKeyValue() returns first key value when the headers argument contains multiple key values for the key name.
        /// </summary>
        [TestMethod]
        public void GetHeaderKeyValuesWithMultipleMatchingHeaderNamesAndMultipleMatchingKeyNames()
        {
            HttpHeaders headers = CreateHeaders();
            headers.Add("HEADER_NAME", "A=a");
            headers.Add("HEADER_NAME", "B=b");
            headers.Add("HEADER_NAME", "C=c1");
            headers.Add("HEADER_NAME", "C=c2");
            Assert.AreEqual("c1", DependencyCollectorDiagnosticListener.GetHeaderKeyValue(headers, "HEADER_NAME", "C"));
        }

        /// <summary>
        /// Ensure that SetHeaderKeyValue() throws an ArgumentNullException when headers is null.
        /// </summary>
        [TestMethod]
        public void SetHeaderKeyValueWithNullHeaders()
        {
            Assert.ThrowsException<ArgumentNullException>(() => DependencyCollectorDiagnosticListener.SetHeaderKeyValue(null, "HEADER_NAME", "KEY_NAME", "KEY_VALUE"));
        }

        /// <summary>
        /// Ensure that SetHeaderKeyValue() sets the proper key value when the headers argument is empty.
        /// </summary>
        [TestMethod]
        public void SetHeaderKeyValueWithEmptyHeaders()
        {
            HttpHeaders headers = CreateHeaders();
            DependencyCollectorDiagnosticListener.SetHeaderKeyValue(headers, "HEADER_NAME", "KEY_NAME", "KEY_VALUE");
            Assert.AreEqual("KEY_VALUE", DependencyCollectorDiagnosticListener.GetHeaderKeyValue(headers, "HEADER_NAME", "KEY_NAME"));
        }

        /// <summary>
        /// Ensure that SetHeaderKeyValue() overwrites an existing key value.
        /// </summary>
        [TestMethod]
        public void SetHeaderKeyValueWithMatchingHeader()
        {
            HttpHeaders headers = CreateHeaders();
            headers.Add("HEADER_NAME", "KEY_NAME=KEY_VALUE1");
            DependencyCollectorDiagnosticListener.SetHeaderKeyValue(headers, "HEADER_NAME", "KEY_NAME", "KEY_VALUE2");
            Assert.AreEqual("KEY_VALUE2", DependencyCollectorDiagnosticListener.GetHeaderKeyValue(headers, "HEADER_NAME", "KEY_NAME"));
        }

        /// <summary>
        /// Ensure that SetHeaderKeyValue() overwrites an existing key value when multiple key name/value pairs exist for a single header.
        /// </summary>
        [TestMethod]
        public void SetHeaderKeyValueWithMultipleMatchingHeaderNamesButOnlyOneMatchingKeyName()
        {
            HttpHeaders headers = CreateHeaders();
            headers.Add("HEADER_NAME", "A=a1");
            headers.Add("HEADER_NAME", "B=b1");
            headers.Add("HEADER_NAME", "C=c1");
            DependencyCollectorDiagnosticListener.SetHeaderKeyValue(headers, "HEADER_NAME", "B", "b2");
            EnumerableAssert.AreEqual(new[] { "A=a1, B=b2, C=c1" }, DependencyCollectorDiagnosticListener.GetHeaderValues(headers, "HEADER_NAME"));
        }

        /// <summary>
        /// Ensure that SetHeaderKeyValue() overwrites all existing key values.
        /// </summary>
        [TestMethod]
        public void SetHeaderKeyValueWithMultipleMatchingHeaderNamesAndMultipleMatchingKeyNames()
        {
            HttpHeaders headers = CreateHeaders();
            headers.Add("HEADER_NAME", "A=a");
            headers.Add("HEADER_NAME", "B=b");
            headers.Add("HEADER_NAME", "C=c1");
            headers.Add("HEADER_NAME", "C=c2");
            DependencyCollectorDiagnosticListener.SetHeaderKeyValue(headers, "HEADER_NAME", "C", "c3");
            EnumerableAssert.AreEqual(new[] { "A=a, B=b, C=c3" }, DependencyCollectorDiagnosticListener.GetHeaderValues(headers, "HEADER_NAME"));
        }

        /// <summary>
        /// Create a HttpHeaders object for testing.
        /// </summary>
        /// <returns></returns>
        private static HttpHeaders CreateHeaders()
        {
            HttpHeaders result = new HttpRequestMessage().Headers;
            Assert.IsNotNull(result);
            return result;
        }
    }
}