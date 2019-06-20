using Microsoft.ApplicationInsights.DataContracts;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Text;

namespace Microsoft.ApplicationInsights.Extensibility.EventCounterCollector.Implementation
{
    /// <summary>
    /// Implementation to listen to EventCounters.
    /// </summary>
    internal class EventCounterListener : EventListener
    {

        // Thread-safe variable to hold the list of all EventSourcesCreated.
        // This class may not be instantiated at the time of EventSource creation, so the list of EventSources should be stored to be enabled after initialization.
        private ConcurrentQueue<EventSource> allEventSourcesCreated;

        private readonly EventLevel level = EventLevel.Critical;
        private bool isInitialized = false;
        private TelemetryClient telemetryClient;

        // EventSourceNames from which counters are to be collected are the keys for this IDictionary.
        // The value will be the corresponding ICollection of counter names.
        private IDictionary<string, ICollection<string>> countersToCollect = new Dictionary<string,ICollection<string>>();

     
        public EventCounterListener(TelemetryClient telemetryClient, IList<EventCounterCollectionRequest> eventCounterCollectionRequests)
        {
            this.telemetryClient = telemetryClient;

            foreach(var collectionRequest in eventCounterCollectionRequests)
            {
                if(!countersToCollect.ContainsKey(collectionRequest.EventSourceName))
                {
                    countersToCollect.Add(collectionRequest.EventSourceName, new HashSet<string>() { collectionRequest.EventCounterName });
                }
                else
                {
                    countersToCollect[collectionRequest.EventSourceName].Add(collectionRequest.EventCounterName);
                }
            }
         
            this.isInitialized = true;

            // Go over every EventSource created before we finished initialization, and enable if required.
            // This will take care of all EventSources created before initialization was done.
            foreach (var eventSource in this.allEventSourcesCreated)
            {
                EnableIfRequired(eventSource);
            }
        }

        /// <summary>
        /// Processes notifications about new EventSource creation.
        /// </summary>
        /// <param name="eventSource">EventSource instance.</param>
        /// <remarks>When an instance of an EventCounterListener is created, it will immediately receive notifications about all EventSources already existing in the AppDomain.
        /// Then, as new EventSources are created, the EventListener will receive notifications about them.</remarks>
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            // Keeping track of all EventSources here, as this call may happen before initialization.
            lock (this)
            {
                if (this.allEventSourcesCreated == null)
                {
                    this.allEventSourcesCreated = new ConcurrentQueue<EventSource>();
                }

                this.allEventSourcesCreated.Enqueue(eventSource);
            }

            // If initialization is already done, we can enable EventSource right away.
            // This will take care of all EventSources created after initialization is done.
            if (this.isInitialized)
            {
                EnableIfRequired(eventSource);
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            // Ignore events if initialization not done yet. We may lose the 1st event if it happens before initialization, in multi-thread situations.
            // Since these are counters, losing the 1st event will not have noticeable impact.
            if (this.isInitialized)
            {
                if (this.countersToCollect.ContainsKey(eventData.EventSource.Name))
                {
                    IDictionary<string, object> eventPayload = eventData.Payload[0] as IDictionary<string, object>;
                    if (eventPayload != null)
                    {
                        extractAndPostMetric(eventData.EventSource.Name,eventPayload);
                    }
                }
            }
        }

        private void EnableIfRequired(EventSource eventSource)
        {
            // The EventSourceName is in the list we want to collect some counters from.
            if (this.countersToCollect.ContainsKey(eventSource.Name))
            {
                Dictionary<string, string> refreshInterval = new Dictionary<string, string>();
                refreshInterval.Add("EventCounterIntervalSec", "60");

                // Unlike regular Events, the only relevant parameter here for EventCounter is the dictionary containing EventCounterIntervalSec.
                EnableEvents(eventSource, level, (EventKeywords)(-1), refreshInterval);
            }
        }

        private void extractAndPostMetric(string eventSourceName, IDictionary<string, object> eventPayload)
        {            
            MetricTelemetry metricTelemetry = new MetricTelemetry();            
            bool calculateRate = false;
            double actualValue = 0.0;
            double actualInterval = 0.0;
            foreach (KeyValuePair<string, object> payload in eventPayload)
            {                                
                var key = payload.Key;
                if (key.Equals("Name"))
                {
                    var counterName = payload.Value.ToString();
                    if (this.countersToCollect[eventSourceName].Contains(counterName))
                    {                        
                        metricTelemetry.Name = counterName;                        
                    }
                    else
                    {
                        // log that ignoring as key not found.
                        return;                        
                    }                    
                }
                else if (key.Equals("Mean"))
                {
                    actualValue = Convert.ToDouble(payload.Value, CultureInfo.InvariantCulture);
                }
                else if (key.Equals("Increment"))
                {
                    // Increment indicates we have to calculate rate.
                    actualValue = Convert.ToDouble(payload.Value, CultureInfo.InvariantCulture);
                    calculateRate = true;
                }                
                else if(key.Equals("IntervalSec"))
                {
                    // Even though we configure 60 sec, we parse the actual duration from here. It'll be very close to the configured interval of 60.
                    actualInterval = Convert.ToDouble(payload.Value, CultureInfo.InvariantCulture);
                }
            }

            if (calculateRate)
            {
                metricTelemetry.Sum = actualValue / actualInterval;
            }
            else
            {
                metricTelemetry.Sum = actualValue;
            }
                        
            // This will make the counter appear under PerformanceCounter as opposed to CustomMetrics in Application Insights Analytics(Kusto) tables.
            metricTelemetry.Properties.Add("CustomPerfCounter", "true");
            this.telemetryClient.TrackMetric(metricTelemetry);
        }
    }
}
