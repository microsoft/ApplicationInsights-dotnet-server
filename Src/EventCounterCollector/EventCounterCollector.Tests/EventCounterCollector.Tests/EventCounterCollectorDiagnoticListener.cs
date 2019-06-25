using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Text;

namespace EventCounterCollector.Tests
{
    internal class EventCounterCollectorDiagnoticListener : EventListener
    {
        public IList<string> EventsReceived { get; private set; }
        public EventCounterCollectorDiagnoticListener()
        {
            this.EventsReceived = new List<string>();
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            this.EventsReceived.Add(eventData.EventName);
        }

        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (string.Equals(eventSource.Name, "Microsoft-ApplicationInsights-Extensibility-EventCounterCollector", StringComparison.Ordinal))
            {
                EnableEvents(eventSource, EventLevel.LogAlways);
            }
        }

    }
}
