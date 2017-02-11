namespace Microsoft.ApplicationInsights.Extensibility.Filtering
{
    using System.Collections.Concurrent;

    internal class AccumulatedValue
    {
        public readonly ConcurrentStack<double> Value = new ConcurrentStack<double>();
        
        public AccumulatedValue(AggregationType aggregationType)
        {
            this.AggregationType = aggregationType;
        }

        public AggregationType AggregationType { get; set; }
    }
}
