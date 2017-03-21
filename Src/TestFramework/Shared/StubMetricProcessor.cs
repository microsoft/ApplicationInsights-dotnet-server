namespace Microsoft.ApplicationInsights.TestFramework
{
    using System;
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.Extensibility.Metrics;

    public class StubMetricProcessor : IMetricProcessor
    {
        public Action<Metric, double> OnTrack = (metric, value) => { };

        public void Track(Metric metric, double value)
        {
            this.OnTrack(metric, value);
        }
    }
}
