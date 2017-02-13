namespace Microsoft.ApplicationInsights.Extensibility.Filtering
{
    using System.Collections.Concurrent;

    /// <summary>
    /// Accumulator for operationalized metrics.
    /// </summary>
    internal class AccumulatedValue
    {
        public readonly ConcurrentStack<double> Value = new ConcurrentStack<double>();
        
        public AccumulatedValue(MetricIdCollection metricIds, AggregationType aggregationType)
        {
            this.MetricIds = metricIds;
            this.AggregationType = aggregationType;
        }

        public MetricIdCollection MetricIds { get; }

        public AggregationType AggregationType { get; }
    }
}
