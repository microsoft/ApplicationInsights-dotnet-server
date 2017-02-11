namespace Microsoft.ApplicationInsights.Extensibility.Filtering
{
    using System;
    using System.Collections.Generic;

    internal class CollectionConfigurationAccumulator
    {
        public long ReferenceCount;

        /// <summary>
        /// (metric.Id, metric.ToString()) => AccumulatedValue
        /// If a metric is being reported under more than one id pair, the same AccumulatedValue object will be associated with both keys in the dictionary.
        /// </summary>
        public Dictionary<Tuple<string, string>, AccumulatedValue> MetricAccumulators { get; } =
            new Dictionary<Tuple<string, string>, AccumulatedValue>();

        public CollectionConfiguration CollectionConfiguration { get; }

        public CollectionConfigurationAccumulator(CollectionConfiguration collectionConfiguration)
        {
            this.CollectionConfiguration = collectionConfiguration;

            // prepare the accumulators based on the collection configuration
            foreach (HashSet<Tuple<string, string>> metricIds in collectionConfiguration.AllMetricIds)
            {
                var accumulatedValue = new AccumulatedValue(AggregationType.Unknown);
                foreach (Tuple<string, string> metricId in metricIds)
                {
                    try
                    {
                        accumulatedValue.AggregationType = collectionConfiguration.GetAggregationType(metricId);
                    }
                    catch (Exception)
                    {
                        // do not set the aggregation type, this metric won't get sent out
                    }

                    this.MetricAccumulators.Add(metricId, accumulatedValue);
                }
            }
        }
    }
}
