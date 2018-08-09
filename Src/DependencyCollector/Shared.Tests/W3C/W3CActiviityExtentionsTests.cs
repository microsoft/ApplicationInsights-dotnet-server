namespace Microsoft.ApplicationInsights.DependencyCollector.W3C
{
    using System.Diagnostics;
    using System.Linq;
    using Microsoft.ApplicationInsights.W3C;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class W3CActiviityExtentionsTests
    {
#pragma warning disable 612, 618
        [TestMethod]
        public void SetInvalidTraceParent()
        {
            var invalidTraceParents = new[]
            {
                "123", string.Empty, null, "00-00", "00-00-00", "00-00-00-", "-00-00-00", "00-00-00-00-00",
                "00-00-00- ", " -00-00-00", "---",  "00---", "00-00--", "00--00-", "00---00"
            };
            foreach (var traceparent in invalidTraceParents)
            {
                var a = new Activity("foo");
                a.SetTraceparent(traceparent);

                Assert.IsFalse(a.IsW3CActivity(), traceparent);
                Assert.IsFalse(a.Tags.Any(t => t.Key == W3CConstants.TraceIdTag), traceparent);
                Assert.IsFalse(a.Tags.Any(t => t.Key == W3CConstants.SpanIdTag), traceparent);
                Assert.IsFalse(a.Tags.Any(t => t.Key == W3CConstants.ParentSpanIdTag), traceparent);
                Assert.IsFalse(a.Tags.Any(t => t.Key == W3CConstants.SampledTag), traceparent);
                Assert.IsFalse(a.Tags.Any(t => t.Key == W3CConstants.VersionTag), traceparent);

                Assert.IsNull(a.GetTraceId());
                Assert.IsNull(a.GetSpanId());
                Assert.IsNull(a.GetParentSpanId());
                Assert.IsNull(a.GetTraceParent());
                Assert.IsNull(a.GetTracestate());
            }
        }

        [TestMethod]
        public void SetValidTraceParent()
        {
            var a = new Activity("foo");
            a.SetTraceparent("12-01010101010101010101010101010101-0202020202020202-ff");

            Assert.IsTrue(a.IsW3CActivity());
            Assert.AreEqual("01010101010101010101010101010101", a.Tags.SingleOrDefault(t => t.Key == W3CConstants.TraceIdTag).Value);
            Assert.AreEqual("0202020202020202", a.Tags.SingleOrDefault(t => t.Key == W3CConstants.ParentSpanIdTag).Value);
            Assert.IsNotNull(a.Tags.SingleOrDefault(t => t.Key == W3CConstants.SpanIdTag));
            Assert.AreEqual(16, a.Tags.Single(t => t.Key == W3CConstants.SpanIdTag).Value.Length);
            Assert.AreEqual("ff", a.Tags.SingleOrDefault(t => t.Key == W3CConstants.SampledTag).Value);
            Assert.AreEqual("12", a.Tags.SingleOrDefault(t => t.Key == W3CConstants.VersionTag).Value);

            Assert.AreEqual("01010101010101010101010101010101", a.GetTraceId());
            Assert.AreEqual("0202020202020202", a.GetParentSpanId());
            Assert.IsNotNull(a.GetSpanId());
            Assert.AreEqual(a.Tags.Single(t => t.Key == W3CConstants.SpanIdTag).Value, a.GetSpanId());
            Assert.AreEqual($"12-01010101010101010101010101010101-{a.GetSpanId()}-ff", a.GetTraceParent());
            Assert.IsNull(a.GetTracestate());
        }

        [TestMethod]
        public void UpdateContextWithoutParent()
        {
            var a = new Activity("foo");

            Assert.IsFalse(a.IsW3CActivity());

            a.UpdateContextOnActivity();
            Assert.IsTrue(a.IsW3CActivity());
            Assert.IsNotNull(a.GetTraceId());
            Assert.IsNotNull(a.GetSpanId());
            Assert.IsNull(a.GetParentSpanId());
            Assert.IsNotNull(a.GetSpanId());

            Assert.AreEqual($"00-{a.GetTraceId()}-{a.GetSpanId()}-02", a.GetTraceParent());
            Assert.IsNull(a.GetTracestate());
        }

        [TestMethod]
        public void UpdateContextWithParent()
        {
            var parent = new Activity("foo").Start();
            parent.SetTraceparent("12-01010101010101010101010101010101-0202020202020202-ff");
            parent.SetTraceState("some=state");
            var child = new Activity("bar").Start();
            child.UpdateContextOnActivity();

            Assert.IsTrue(child.IsW3CActivity());
            Assert.AreEqual("01010101010101010101010101010101", child.GetTraceId());
            Assert.AreEqual(parent.GetSpanId(), child.GetParentSpanId());
            Assert.AreEqual($"12-01010101010101010101010101010101-{child.GetSpanId()}-ff", child.GetTraceParent());
            Assert.AreEqual(parent.GetTracestate(), child.GetTracestate());
        }
#pragma warning restore 612, 618
    }
}
