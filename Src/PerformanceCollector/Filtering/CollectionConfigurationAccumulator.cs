namespace Microsoft.ApplicationInsights.Extensibility.Filtering
{
    using System;
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

        public CollectionConfigurationAccumulator(CollectionConfiguration collectionConfiguration)
        {
            this.CollectionConfiguration = collectionConfiguration;

            // prepare the accumulators based on the collection configuration
            foreach (Tuple<string, AggregationType> metricId in
                collectionConfiguration?.TelemetryMetadata.Concat(collectionConfiguration.MetricMetadata)
                ?? Enumerable.Empty<Tuple<string, AggregationType>>())
            {
                var accumulatedValue = new AccumulatedValue(metricId.Item1, metricId.Item2);

                this.MetricAccumulators.Add(metricId.Item1, accumulatedValue);
            }
        }

        /// <summary>
        /// Gets a dictionary of metricId => AccumulatedValue.
        /// </summary>
        public Dictionary<string, AccumulatedValue> MetricAccumulators { get; } = new Dictionary<string, AccumulatedValue>();

        public CollectionConfiguration CollectionConfiguration { get; }
    }
}