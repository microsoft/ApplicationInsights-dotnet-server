namespace Microsoft.ApplicationInsights.Web
{
    using System.Collections.Generic;
    using System.Web;    
    using Common;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Web.Helpers;
    using Microsoft.ApplicationInsights.Web.Implementation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;    
    
    [TestClass]
    public class OperationCorrelationTelemetryInitializerTests
    {
        [TestMethod]
        public void InitializeDoesNotThrowWhenHttpContextIsNull()
        {
            var source = new OperationCorrelationTelemetryInitializer();
            source.Initialize(new RequestTelemetry());
        }

        [TestMethod]
        public void InitializeSetsParentIdForTelemetryUsingIdFromRequestTelemetry()
        {
            var exceptionTelemetry = new ExceptionTelemetry();
            var source = new TestableOperationCorrelationTelemetryInitializer(null);
            var requestTelemetry = source.Telemetry;

            source.Initialize(exceptionTelemetry);

            Assert.AreEqual(requestTelemetry.Id, exceptionTelemetry.Context.Operation.ParentId);
        }

        [TestMethod]
        public void InitializeDoesNotOverrideCustomerParentOperationId()
        {
            var source = new TestableOperationCorrelationTelemetryInitializer(null);

            var customerTelemetry = new TraceTelemetry("Text");
            customerTelemetry.Context.Operation.ParentId = "CustomId";

            source.Initialize(customerTelemetry);

            Assert.AreEqual("CustomId", customerTelemetry.Context.Operation.ParentId);
        }

        [TestMethod]
        public void InitializeSetsRootOperationIdForTelemetryUsingIdFromRequestTelemetry()
        {
            var exceptionTelemetry = new ExceptionTelemetry();
            var source = new TestableOperationCorrelationTelemetryInitializer(null);
            var requestTelemetry = source.Telemetry;
            requestTelemetry.Context.Operation.Id = "RootId";

            source.Initialize(exceptionTelemetry);

            Assert.AreEqual(requestTelemetry.Context.Operation.Id, exceptionTelemetry.Context.Operation.Id);
        }

        [TestMethod]
        public void InitializeDoesNotOverrideCustomerRootOperationId()
        {
            var source = new TestableOperationCorrelationTelemetryInitializer(null);
            var requestTelemetry = source.Telemetry;
            requestTelemetry.Context.Operation.Id = "RootId";

            var customerTelemetry = new TraceTelemetry("Text");
            customerTelemetry.Context.Operation.Id = "CustomId";

            source.Initialize(customerTelemetry);

            Assert.AreEqual("CustomId", customerTelemetry.Context.Operation.Id);
        }

        [TestMethod]
        public void InitializeSetsRequestTelemetryRootOperaitonIdToOperaitonId()
        {
            var source = new TestableOperationCorrelationTelemetryInitializer(null);
            var requestTelemetry = source.Telemetry;

            var customerTelemetry = new TraceTelemetry("Text");

            source.Initialize(customerTelemetry);

            Assert.AreEqual(AppInsightsActivity.GetRootId(requestTelemetry.Id), requestTelemetry.Context.Operation.Id);
        }

        [TestMethod]
        public void InitializeReadsParentIdFromCustomHeader()
        {
            var source = new TestableOperationCorrelationTelemetryInitializer(new Dictionary<string, string>() { { "headerName", "ParentId" } });
            source.ParentOperationIdHeaderName = "headerName";
            var requestTelemetry = source.Telemetry;

            var customerTelemetry = new TraceTelemetry("Text");

            source.Initialize(customerTelemetry);

            Assert.AreEqual("ParentId", requestTelemetry.Context.Operation.ParentId);
        }

        [TestMethod]
        public void InitializeReadsRootIdFromCustomHeader()
        {
            var source = new TestableOperationCorrelationTelemetryInitializer(new Dictionary<string, string>() { { "headerName", "RootId" } });
            source.RootOperationIdHeaderName = "headerName";
            var requestTelemetry = source.Telemetry;

            var customerTelemetry = new TraceTelemetry("Text");

            source.Initialize(customerTelemetry);
            Assert.AreEqual("RootId", customerTelemetry.Context.Operation.Id);

            Assert.AreEqual("RootId", requestTelemetry.Context.Operation.Id);
        }

        [TestMethod]
        public void InitializeDoNotMakeRequestAParentOfItself()
        {
            var source = new TestableOperationCorrelationTelemetryInitializer(null);
            var requestTelemetry = source.Telemetry;

            source.Initialize(requestTelemetry);
            Assert.AreEqual(null, requestTelemetry.Context.Operation.ParentId);
            Assert.AreEqual(AppInsightsActivity.GetRootId(requestTelemetry.Id), requestTelemetry.Context.Operation.Id);
        }

        private class TestableOperationCorrelationTelemetryInitializer : OperationCorrelationTelemetryInitializer
        {
            private readonly HttpContext fakeContext;
            private readonly RequestTelemetry telemetry;

            public TestableOperationCorrelationTelemetryInitializer(IDictionary<string, string> headers)
            {
                 this.fakeContext = HttpModuleHelper.GetFakeHttpContext(headers);
                telemetry = fakeContext.SetOperationHolder().Telemetry;
            }

            public HttpContext FakeContext
            {
                get { return this.fakeContext; }
            }

            public RequestTelemetry Telemetry
            {
                get { return this.telemetry; }
            }

            protected override HttpContext ResolvePlatformContext()
            {
                return this.fakeContext;
            }
        }
    }
}