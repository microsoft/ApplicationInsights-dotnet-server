namespace Microsoft.ApplicationInsights.DependencyCollector.W3C
{
    using System.Diagnostics;
    using System.Linq;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;
    using Microsoft.ApplicationInsights.W3C;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable 612, 618
    [TestClass]
    public class W3COperationCorrelationTelemetryInitializerTests
    {
        [TestCleanup]
        public void Cleanup()
        {
            while (Activity.Current != null)
            {
                Activity.Current.Stop();
            }
        }

        [TestMethod]
        public void InitializerCreatesNewW3CContext()
        {
            Activity a = new Activity("dummy")
                .Start();

            RequestTelemetry request = new RequestTelemetry();
            string expectedId = request.Id;

            new W3COperationCorrelationTelemetryInitializer().Initialize(request);

            Assert.IsNotNull(request.Context.Operation.Id);
            Assert.IsNull(request.Context.Operation.ParentId);
            Assert.AreNotEqual(expectedId, request.Id);
            Assert.IsFalse(request.Properties.Any());
        }

        [TestMethod]
        public void InitializerSetsCorrelationIdsOnTraceTelemetry()
        {
            Activity a = new Activity("dummy")
                .Start()
                .GenerateW3CContext();
            
            string expectedTrace = a.GetTraceId();
            string expectedParent = a.GetSpanId();

            TraceTelemetry trace = new TraceTelemetry();
            new W3COperationCorrelationTelemetryInitializer().Initialize(trace);

            Assert.AreEqual(expectedTrace, trace.Context.Operation.Id);
            Assert.AreEqual(expectedParent, trace.Context.Operation.ParentId);

            Assert.IsFalse(trace.Properties.Any());
        }

        [TestMethod]
        public void InitializerSetsCorrelationIdsOnRequestTelemetry()
        {
            Activity a = new Activity("dummy")
                .Start()
                .GenerateW3CContext();

            string expectedTrace = a.GetTraceId();
            string expectedId = a.GetSpanId();

            string expectedParent = "0123456789abcdef";
            a.AddTag(W3CConstants.ParentSpanIdTag, expectedParent);

            RequestTelemetry request = new RequestTelemetry();
            new W3COperationCorrelationTelemetryInitializer().Initialize(request);

            Assert.AreEqual(expectedTrace, request.Context.Operation.Id);
            Assert.AreEqual(expectedParent, request.Context.Operation.ParentId);
            Assert.AreEqual(expectedId, request.Id);

            Assert.IsFalse(request.Properties.Any());
        }

        [TestMethod]
        public void InitializerSetsCorrelationIdsOnRequestTelemetryNoParent()
        {
            Activity a = new Activity("dummy")
                .Start()
                .GenerateW3CContext();

            string expectedTrace = a.GetTraceId();
            string expectedId = a.GetSpanId();

            RequestTelemetry request = new RequestTelemetry();
            new W3COperationCorrelationTelemetryInitializer().Initialize(request);

            Assert.AreEqual(expectedTrace, request.Context.Operation.Id);
            Assert.IsNull(request.Context.Operation.ParentId);
            Assert.AreEqual(expectedId, request.Id);

            Assert.IsFalse(request.Properties.Any());
        }

        [TestMethod]
        public void InitializerNoopWithoutActivity()
        {
            RequestTelemetry request = new RequestTelemetry();
            new W3COperationCorrelationTelemetryInitializer().Initialize(request);

            Assert.IsNull(request.Context.Operation.Id);
            Assert.IsNull(request.Context.Operation.ParentId);

            Assert.IsFalse(request.Properties.Any());
        }

        [TestMethod]
        public void InitializerIgnoresExistingValues()
        {
            Activity a = new Activity("dummy")
                .Start()
                .GenerateW3CContext();

            string expectedTrace = a.GetTraceId();
            string expectedId = a.GetSpanId();

            string expectedParent = "0123456789abcdef";
            a.AddTag(W3CConstants.ParentSpanIdTag, expectedParent);

            RequestTelemetry request = new RequestTelemetry();

            request.Context.Operation.Id = "operation id";
            request.Context.Operation.ParentId = "parent id";
            request.Id = "id";

            new W3COperationCorrelationTelemetryInitializer().Initialize(request);

            Assert.AreEqual(expectedTrace, request.Context.Operation.Id);
            Assert.AreEqual(expectedParent, request.Context.Operation.ParentId);
            Assert.AreEqual(expectedId, request.Id);
        }

        [TestMethod]
        public void InitializerDoesNotPopulateTraceStateOnTelemetry()
        {
            Activity a = new Activity("dummy")
                .Start()
                .GenerateW3CContext();
                
            a.SetTraceState("key=value");

            string expectedTrace = a.GetTraceId();
            string expectedId = a.GetSpanId();

            RequestTelemetry request = new RequestTelemetry();

            new W3COperationCorrelationTelemetryInitializer().Initialize(request);

            Assert.AreEqual(expectedTrace, request.Context.Operation.Id);
            Assert.AreEqual(expectedId, request.Id);

            Assert.IsFalse(request.Properties.Any());
        }

        [TestMethod]
        public void InitializerOnNestedActivitities()
        {
            Activity requestActivity = new Activity("request")
                .Start();

            RequestTelemetry request = new RequestTelemetry();
            new W3COperationCorrelationTelemetryInitializer().Initialize(request);

            Activity nested1 = new Activity("nested1").Start();
            Activity nested2 = new Activity("nested1").Start();

            DependencyTelemetry dependency2 = new DependencyTelemetry();
            new W3COperationCorrelationTelemetryInitializer().Initialize(dependency2);

            Assert.AreEqual(request.Context.Operation.Id, nested2.GetTraceId());
            Assert.AreEqual(request.Context.Operation.Id, nested1.GetTraceId());

            Assert.AreEqual(request.Id, nested1.GetParentSpanId());
            Assert.AreEqual(nested1.GetSpanId(), nested2.GetParentSpanId());

            Assert.AreEqual(request.Context.Operation.Id, dependency2.Context.Operation.Id);

            nested2.Stop();

            DependencyTelemetry dependency1 = new DependencyTelemetry();
            new W3COperationCorrelationTelemetryInitializer().Initialize(dependency1);

            Assert.AreEqual(dependency2.Context.Operation.ParentId, dependency1.Id);
            Assert.AreEqual(request.Context.Operation.Id, dependency1.Context.Operation.Id);
            Assert.AreEqual(request.Id, dependency1.Context.Operation.ParentId);
        }

        [TestMethod]
        public void InitializerOnSqlDepenedency()
        {
            Activity requestActivity = new Activity("request")
                .Start()
                .GenerateW3CContext();

            RequestTelemetry request = new RequestTelemetry();
            DependencyTelemetry sqlDependency = new DependencyTelemetry()
            {
                Type = "SQL"
            };
            sqlDependency.Context.GetInternalContext().SdkVersion = "rdddsc:12345";
            string expectedId = sqlDependency.Id;

            new W3COperationCorrelationTelemetryInitializer().Initialize(sqlDependency);
            new W3COperationCorrelationTelemetryInitializer().Initialize(request);

            Assert.AreEqual(request.Context.Operation.Id, sqlDependency.Context.Operation.Id);
            Assert.AreEqual(request.Id, sqlDependency.Context.Operation.ParentId);
            Assert.AreEqual(expectedId, sqlDependency.Id);
        }
    }
#pragma warning restore 612, 618
}
