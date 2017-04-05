namespace Microsoft.ApplicationInsights.Extensibility.Filtering
{
    using System.Collections.Concurrent;

    /// <summary>
    /// Accumulator for calculated metrics.
    /// </summary>
    internal class AccumulatedValue
    {
        public readonly ConcurrentStack<double> Value = new ConcurrentStack<double>();

        public AccumulatedValue(string metricId, AggregationType aggregationType)
        {
            this.MetricId = metricId;
            this.AggregationType = aggregationType;
        }

        public string MetricId { get; }

        public AggregationType AggregationType { get; }
    }
}