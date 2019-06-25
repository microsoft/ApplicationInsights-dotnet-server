using EventCounterCollector.Tests;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.Channel;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.EventCounterCollector;
using Microsoft.ApplicationInsights.Extensibility.EventCounterCollector.Implementation;
using Microsoft.ApplicationInsights.Extensibility.Implementation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace EventCounterCollector.Tests
{
    [TestClass]
    public class EventCounterCollectionModuleTests
    {
        private string TestEventCounterSourceName = "Microsoft-ApplicationInsights-Extensibility-EventCounterCollector.Tests.TestEventCounter";
        private string TestEventCounterName1 = "mycountername1";

        [TestMethod]
        [TestCategory("EventCounter")]
        public void WarnsIfNoCountersConfigured()
        {
            using (var eventListener = new EventCounterCollectorDiagnoticListener())
            using (var module = new EventCounterCollectionModule())
            {
                List<ITelemetry> itemsReceived = new List<ITelemetry>();
                module.Initialize(GetTestTelemetryConfiguration(itemsReceived));
                Assert.IsTrue(CheckEventReceived(eventListener.EventsReceived, nameof(EventCounterCollectorEventSource.EventCounterCollectorNoCounterConfigured)));
            }
        }

        [TestMethod]
        [TestCategory("EventCounter")]
        public void ValidateSingleEventCounterCollection()
        {
            // ARRANGE
            const double refreshTimeInSecs = 1;
            List<ITelemetry> itemsReceived = new List<ITelemetry>();
            string expectedName = this.TestEventCounterSourceName + "|" + this.TestEventCounterName1;
            double expectedMetricValue = (1000 + 1500 + 1500 + 400) / 4;

            using (var module = new EventCounterCollectionModule(refreshTimeInSecs))
            {
                module.Counters.Add(new EventCounterCollectionRequest() {EventSourceName = this.TestEventCounterSourceName, EventCounterName = this.TestEventCounterName1 });
                module.Initialize(GetTestTelemetryConfiguration(itemsReceived));

                // ACT
                // Making 3 calls with 1000, 1500, 1500, 400 value, leading to an avge of 1100.
                TestEventCounter.Log.SampleCounter1(1000);
                TestEventCounter.Log.SampleCounter1(1500);
                TestEventCounter.Log.SampleCounter1(1500);
                TestEventCounter.Log.SampleCounter1(400);

                // Wait atleast for refresh time.
                Task.Delay(((int) refreshTimeInSecs * 1000) + 500).Wait();

                PrintTelemetryItems(itemsReceived);

                // VALIDATE
                MetricTelemetry telemetry = itemsReceived[0] as MetricTelemetry;
                ValidateTelemetry(telemetry, expectedName, expectedMetricValue);

                // Clear the items.
                itemsReceived.Clear();

                // Wait another refreshinterval to receive more events, but with zero as counter values.
                // as nobody is publishing events.
                Task.Delay(((int)refreshTimeInSecs * 2000)).Wait();                
                Assert.IsTrue(itemsReceived.Count >= 1);
                PrintTelemetryItems(itemsReceived);
                telemetry = itemsReceived[0] as MetricTelemetry;
                ValidateTelemetry(telemetry, expectedName, 0.0);
            }
            
            
        }

        private void ValidateTelemetry(MetricTelemetry metricTelemetry, string expectedName, double expectedSum)
        {
            Assert.IsTrue(metricTelemetry.Context.GetInternalContext().SdkVersion.StartsWith("evtc"));
            Assert.AreEqual(expectedSum, metricTelemetry.Sum);
            Assert.AreEqual(expectedName, metricTelemetry.Name);
        }

        private void PrintTelemetryItems(IList<ITelemetry> telemetry)
        {
            Trace.WriteLine("Received count:" + telemetry.Count);
            foreach (var item in telemetry)
            {
                var metric = item as MetricTelemetry;
                Trace.WriteLine("Metric.Name:" + metric.Name);
                Trace.WriteLine("Metric.Sum:" + metric.Sum);
                Trace.WriteLine("Metric.Count:" + metric.Count);
                Trace.WriteLine("Metric.Timestamp:" + metric.Timestamp.ToString());
                Trace.WriteLine("Metric.Sdk:" + metric.Context.GetInternalContext().SdkVersion);
                foreach (var prop in metric.Properties)
                {
                    Trace.WriteLine("Metric. Prop:" + "Key:"+ prop.Key + "Value:" + prop.Value );
                }
                Trace.WriteLine("======================================");
            }
        }

        private bool CheckEventReceived(IList<string> allEvents, string expectedEvent)
        {
            bool found = false;
            foreach(var evt in allEvents)
            {
                if(evt.Equals(expectedEvent))
                {
                    found = true;
                    break;
                }
            }

            return found;
        }

        private TelemetryConfiguration GetTestTelemetryConfiguration(List<ITelemetry> itemsReceived)
        {
            var configuration = new TelemetryConfiguration();
            configuration.InstrumentationKey = "testkey";
            configuration.TelemetryChannel = new TestChannel(itemsReceived);
            return configuration;
        }
    }
}
