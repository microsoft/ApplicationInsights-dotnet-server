namespace Microsoft.ApplicationInsights.Extensibility.EventCounterCollector.Implementation
    {
    using System.Diagnostics.CodeAnalysis;
    using System.Diagnostics.Tracing;
    using Microsoft.ApplicationInsights.Common;

    [EventSource(Name = "Microsoft-ApplicationInsights-Extensibility-PerformanceCollector")]
    [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", Justification = "appDomainName is required")]
    internal sealed class EventCounterCollectorEventSource : EventSource
    {
        private readonly ApplicationNameProvider applicationNameProvider = new ApplicationNameProvider();

        private EventCounterCollectorEventSource()
        {
        }

        public static EventCounterCollectorEventSource Log { get; } = new EventCounterCollectorEventSource();
        

        [Event(1, Level = EventLevel.Informational, Message = @"EventCounterCollectionModule is being initialized. {0}")]
        public void ModuleIsBeingInitializedEvent(
            string message,
            string applicationName = "dummy")
        {
            this.WriteEvent(1, message, this.applicationNameProvider.Name);
        }

        [Event(2, Level = EventLevel.Informational, Message = @"EventCounterCollectionModule has been successfully initialized.")]
        public void ModuleInitializedSuccess(string applicationName = "dummy")
        {
            this.WriteEvent(2, this.applicationNameProvider.Name);
        }

        [Event(3, Level = EventLevel.Warning, Message = @"EventCounterCollectionModule - {0} failed with exception: {1}.")]
        public void ModuleException(string stage, string exceptionMessage, string applicationName = "dummy")
        {
            this.WriteEvent(3, stage, exceptionMessage, this.applicationNameProvider.Name);
        }

        [Event(4, Level = EventLevel.Informational, Message = @"Performance counters have been refreshed. Refreshed counters count is {0}.")]
        public void CountersRefreshedEvent(
            string countersRefreshedCount,
            string applicationName = "dummy")
        {
            this.WriteEvent(4, countersRefreshedCount, this.applicationNameProvider.Name);
        }


        public class Keywords
        {
            public const EventKeywords UserActionable = (EventKeywords)0x1;

            public const EventKeywords Diagnostics = (EventKeywords)0x2;
        }
    }
}