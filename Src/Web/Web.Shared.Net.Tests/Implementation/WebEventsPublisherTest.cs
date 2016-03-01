namespace Microsoft.ApplicationInsights.Web.Implementation
{
    using System;

#if NET45
    using System.Diagnostics.Tracing;
#endif

    using Microsoft.ApplicationInsights.Web.TestFramework;

#if NET40
    using Microsoft.Diagnostics.Tracing;
#endif

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class WebEventsPublisherTest
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void WriteThrowsIfNullOwnerIsPassed()
        {
            WebEventsPublisher.Log.Write(null, 1);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void WriteThrowsIfIncorrectIdPassed()
        {
            WebEventsPublisher.Log.Write(new object(), 100);
        }

        [TestMethod]
        public void WritePublishesMultipleEvents()
        {
            // Check that publishing works not only when we locked the object but later when object is the same

            using (var listener = new TestEventListener())
            {
                const long AllKeyword = -1;
                listener.EnableEvents(WebEventsPublisher.Log, EventLevel.LogAlways, (EventKeywords)AllKeyword);

                object obj1 = new object();
                WebEventsPublisher.Log.Write(obj1, 1);
                WebEventsPublisher.Log.Write(obj1, 2);
                WebEventsPublisher.Log.Write(obj1, 3);

                Assert.AreEqual(3, listener.Messages.Count);

                // cleanup
                WebEventsPublisher.Log.Release(obj1);
            }
        }

        [TestMethod]
        public void WritePublishesEventsOnlyForFirstOwner()
        {
            using (var listener = new TestEventListener())
            {
                const long AllKeyword = -1;
                listener.EnableEvents(WebEventsPublisher.Log, EventLevel.LogAlways, (EventKeywords)AllKeyword);

                object obj1 = new object();
                WebEventsPublisher.Log.Write(obj1, 1);

                object obj2 = new object();
                WebEventsPublisher.Log.Write(obj2, 1);

                Assert.AreEqual(1, listener.Messages.Count);

                // cleanup
                WebEventsPublisher.Log.Release(obj1);
                WebEventsPublisher.Log.Release(obj2);
            }
        }

        [TestMethod]
        public void WritePublishesEventsOnlyForSecondOwnerIfFirstReleased()
        {
            using (var listener = new TestEventListener())
            {
                const long AllKeyword = -1;
                listener.EnableEvents(WebEventsPublisher.Log, EventLevel.LogAlways, (EventKeywords)AllKeyword);

                object obj1 = new object();
                WebEventsPublisher.Log.Write(obj1, 1);
                object obj2 = new object();
                WebEventsPublisher.Log.Write(obj2, 1);
                listener.Messages.Clear();

                WebEventsPublisher.Log.Release(obj1);

                WebEventsPublisher.Log.Write(obj2, 1);

                Assert.AreEqual(1, listener.Messages.Count);

                // cleanup
                WebEventsPublisher.Log.Release(obj1);
                WebEventsPublisher.Log.Release(obj2);
            }
        }

        [TestMethod]
        public void WritePublishesOnBegin()
        {
            using (var listener = new TestEventListener())
            {
                const long AllKeyword = -1;
                listener.EnableEvents(WebEventsPublisher.Log, EventLevel.LogAlways, (EventKeywords)AllKeyword);

                object obj1 = new object();
                WebEventsPublisher.Log.Write(obj1, 1);
                
                Assert.AreEqual(1, listener.Messages[0].EventId);

                // cleanup
                WebEventsPublisher.Log.Release(obj1);
            }
        }

        [TestMethod]
        public void WritePublishesOnEnd()
        {
            using (var listener = new TestEventListener())
            {
                const long AllKeyword = -1;
                listener.EnableEvents(WebEventsPublisher.Log, EventLevel.LogAlways, (EventKeywords)AllKeyword);

                object obj1 = new object();
                WebEventsPublisher.Log.Write(obj1, 2);
                
                Assert.AreEqual(2, listener.Messages[0].EventId);

                // cleanup
                WebEventsPublisher.Log.Release(obj1);
            }
        }

        [TestMethod]
        public void WritePublishesOnError()
        {
            using (var listener = new TestEventListener())
            {
                const long AllKeyword = -1;
                listener.EnableEvents(WebEventsPublisher.Log, EventLevel.LogAlways, (EventKeywords)AllKeyword);

                object obj1 = new object();
                WebEventsPublisher.Log.Write(obj1, 3);

                Assert.AreEqual(3, listener.Messages[0].EventId);

                // cleanup
                WebEventsPublisher.Log.Release(obj1);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void ReleaseThrowsIfNullOwnerIsPassed()
        {
            WebEventsPublisher.Log.Release(null);
        }

        [TestMethod]
        public void ReleasIgnoresNotAnOwner()
        {
            using (var listener = new TestEventListener())
            {
                const long AllKeyword = -1;
                listener.EnableEvents(WebEventsPublisher.Log, EventLevel.LogAlways, (EventKeywords)AllKeyword);

                object obj1 = new object();
                WebEventsPublisher.Log.Write(obj1, 1);
                object obj2 = new object();
                WebEventsPublisher.Log.Write(obj2, 1);
                listener.Messages.Clear();

                WebEventsPublisher.Log.Release(obj2);

                WebEventsPublisher.Log.Write(obj1, 1);

                Assert.AreEqual(1, listener.Messages.Count);

                // cleanup
                WebEventsPublisher.Log.Release(obj1);
                WebEventsPublisher.Log.Release(obj2);
            }
        }
    }
}
