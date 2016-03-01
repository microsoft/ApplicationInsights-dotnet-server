namespace Microsoft.ApplicationInsights.Web
{
    using System;
#if NET45
    using System.Diagnostics.Tracing;
#endif
    using System.Globalization;
    using System.Linq;
    using System.Web;

    using Microsoft.ApplicationInsights.Web.Helpers;
    using Microsoft.ApplicationInsights.Web.Implementation;
    using Microsoft.ApplicationInsights.Web.TestFramework;
#if NET40
    using Microsoft.Diagnostics.Tracing;
#endif
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class ApplicationInsightsHttpModuleTests
    {
        private const long AllKeywords = -1;

        private PrivateObject module1;
        private PrivateObject module2;

        [TestInitialize]
        public void Initialize()
        {
            this.module1 = HttpModuleHelper.CreateTestModule();
            this.module2 = HttpModuleHelper.CreateTestModule();
        }

        [TestCleanup]
        public void Cleanup()
        {
            ((IHttpModule)this.module1.Target).Dispose();
            ((IHttpModule)this.module2.Target).Dispose();
        }

        [TestMethod]
        public void OnBeginGeneratesWebEventsOnBeginEvent()
        {
            using (var listener = new TestEventListener())
            {
                listener.EnableEvents(WebEventsPublisher.Log, EventLevel.LogAlways, (EventKeywords)AllKeywords);

                this.module1.Invoke("OnBeginRequest", new[] { typeof(object), typeof(EventArgs) }, new object[] { HttpModuleHelper.GetFakeHttpApplication(), null }, CultureInfo.InvariantCulture);

                var firstEvent = listener.Messages.FirstOrDefault();
                Assert.IsNotNull(firstEvent);
                Assert.AreEqual(1, firstEvent.EventId);
            }
        }

        [TestMethod]
        public void OnBeginGeneratesEventsOnlyFromOneModuleInstance()
        {
            using (var listener = new TestEventListener())
            {
                listener.EnableEvents(WebEventsPublisher.Log, EventLevel.LogAlways, (EventKeywords)AllKeywords);

                this.module1.Invoke("OnBeginRequest", new[] { typeof(object), typeof(EventArgs) }, new object[] { HttpModuleHelper.GetFakeHttpApplication(), null }, CultureInfo.InvariantCulture);
                this.module2.Invoke("OnBeginRequest", new[] { typeof(object), typeof(EventArgs) }, new object[] { HttpModuleHelper.GetFakeHttpApplication(), null }, CultureInfo.InvariantCulture);

                var count = listener.Messages.Count();
                Assert.AreEqual(1, count);
            }
        }

        [TestMethod]
        public void OnBeginGeneratesEventsOnlyFromSecondModuleIfFirstOneDisposed()
        {
            using (var listener = new TestEventListener())
            {
                listener.EnableEvents(WebEventsPublisher.Log, EventLevel.LogAlways, (EventKeywords)AllKeywords);

                this.module1.Invoke("OnBeginRequest", new[] { typeof(object), typeof(EventArgs) }, new object[] { HttpModuleHelper.GetFakeHttpApplication(), null }, CultureInfo.InvariantCulture);
                ((IHttpModule)this.module1.Target).Dispose();
                listener.Messages.Clear();

                this.module2.Invoke("OnBeginRequest", new[] { typeof(object), typeof(EventArgs) }, new object[] { HttpModuleHelper.GetFakeHttpApplication(), null }, CultureInfo.InvariantCulture);

                var count = listener.Messages.Count();
                Assert.AreEqual(1, count);
            }
        }

        [TestMethod]
        public void OnEndGeneratesWebEventsOnEndEvent()
        {
            using (var listener = new TestEventListener())
            {
                listener.EnableEvents(WebEventsPublisher.Log, EventLevel.LogAlways, (EventKeywords)AllKeywords);

                this.module1.Invoke("OnEndRequest", new[] { typeof(object), typeof(EventArgs) }, new object[] { HttpModuleHelper.GetFakeHttpApplication(), null }, CultureInfo.InvariantCulture);

                var messages = listener.Messages.OrderBy(_ => _.EventId).ToList();
                Assert.AreEqual(2, messages[0].EventId);
            }
        }

        [TestMethod]
        public void OnEndGeneratesWebEventsOnErrorEvent()
        {
            using (var listener = new TestEventListener())
            {
                listener.EnableEvents(WebEventsPublisher.Log, EventLevel.LogAlways, (EventKeywords)AllKeywords);

                this.module1.Invoke("OnEndRequest", new[] { typeof(object), typeof(EventArgs) }, new object[] { HttpModuleHelper.GetFakeHttpApplication(), null }, CultureInfo.InvariantCulture);

                var messages = listener.Messages.OrderBy(_ => _.EventId).ToList();
                Assert.AreEqual(3, messages[1].EventId);
            }
        }

        [TestMethod]
        public void OnEndGeneratesEndEventsOnlyFromOneModuleInstance()
        {
            using (var listener = new TestEventListener())
            {
                listener.EnableEvents(WebEventsPublisher.Log, EventLevel.LogAlways, (EventKeywords)AllKeywords);

                this.module1.Invoke("OnEndRequest", new[] { typeof(object), typeof(EventArgs) }, new object[] { HttpModuleHelper.GetFakeHttpApplication(), null }, CultureInfo.InvariantCulture);
                this.module2.Invoke("OnEndRequest", new[] { typeof(object), typeof(EventArgs) }, new object[] { HttpModuleHelper.GetFakeHttpApplication(), null }, CultureInfo.InvariantCulture);

                var count = listener.Messages.Count(item => item.EventId == 2);
                Assert.AreEqual(1, count);
            }
        }

        [TestMethod]
        public void OnEndGeneratesErrorEventsOnlyFromOneModuleInstance()
        {
            using (var listener = new TestEventListener())
            {
                listener.EnableEvents(WebEventsPublisher.Log, EventLevel.LogAlways, (EventKeywords)AllKeywords);

                this.module1.Invoke("OnEndRequest", new[] { typeof(object), typeof(EventArgs) }, new object[] { HttpModuleHelper.GetFakeHttpApplication(), null }, CultureInfo.InvariantCulture);
                this.module2.Invoke("OnEndRequest", new[] { typeof(object), typeof(EventArgs) }, new object[] { HttpModuleHelper.GetFakeHttpApplication(), null }, CultureInfo.InvariantCulture);

                var count = listener.Messages.Count(item => item.EventId == 3);
                Assert.AreEqual(1, count);
            }
        }
    }
}