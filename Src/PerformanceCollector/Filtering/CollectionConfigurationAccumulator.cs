namespace Microsoft.ApplicationInsights.Extensibility.Filtering
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Represents the operationalized metric-related component of the QuickPulse accumulator.
    /// Unlike the main accumulator, this one might not have finished being processed at swap time,
    /// so the consumer should keep the reference to it post-swap and make the best effort not to send
    /// prematurely. <see cref="ReferenceCount"/> indicates that the accumulator is still being processed
    /// when non-zero.
    /// </summary>
    internal class CollectionConfigurationAccumulator
    {
        /// <summary>
        /// Used by writers to indicate that a processing operation is still in progress.
        /// </summary>
        public long ReferenceCount = 0;

        /// <summary>
        /// (metric.SessionId, metric.Id) => AccumulatedValue
        /// If a metric is being reported under more than one id pair, the same AccumulatedValue object will be associated with multiple keys in the dictionary.
        /// </summary>
        public Dictionary<Tuple<string, string>, AccumulatedValue> MetricAccumulators { get; } =
            new Dictionary<Tuple<string, string>, AccumulatedValue>();

        public CollectionConfiguration CollectionConfiguration { get; }

        public CollectionConfigurationAccumulator(CollectionConfiguration collectionConfiguration)
        {
            this.CollectionConfiguration = collectionConfiguration;

            // prepare the accumulators based on the collection configuration
            foreach (
                Tuple<MetricIdCollection, AggregationType> metricIds in
                    collectionConfiguration?.MetricMetadata ?? Enumerable.Empty<Tuple<MetricIdCollection, AggregationType>>())
            {
                var accumulatedValue = new AccumulatedValue(metricIds.Item1, metricIds.Item2);

                // when reporting the same metric under multiple id pairs, use the same accumulator for all
                this.MetricAccumulators.Add(metricIds.Item1.First(), accumulatedValue);
            }
        }
    }
}
