namespace Microsoft.ApplicationInsights.Extensibility.Filtering
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.Implementation.QuickPulse;

    internal class CollectionConfiguration
    {
        private readonly CollectionConfigurationInfo info;

        //!!! replace HashSet<Tuple<string,string>> with a separate class
        
        private readonly Dictionary<string, OperationalizedMetric<RequestTelemetry>> requestTelemetryMetrics =
            new Dictionary<string, OperationalizedMetric<RequestTelemetry>>();

        private readonly Dictionary<string, OperationalizedMetric<DependencyTelemetry>> dependencyTelemetryMetrics =
            new Dictionary<string, OperationalizedMetric<DependencyTelemetry>>();

        private readonly Dictionary<string, OperationalizedMetric<ExceptionTelemetry>> exceptionTelemetryMetrics =
            new Dictionary<string, OperationalizedMetric<ExceptionTelemetry>>();

        private readonly Dictionary<string, OperationalizedMetric<EventTelemetry>> eventTelemetryMetrics =
            new Dictionary<string, OperationalizedMetric<EventTelemetry>>();

        private readonly List<HashSet<Tuple<string, string>>> allMetricIds = new List<HashSet<Tuple<string, string>>>();

        public IEnumerable<OperationalizedMetric<RequestTelemetry>> RequestMetrics => this.requestTelemetryMetrics.Values;

        public IEnumerable<OperationalizedMetric<DependencyTelemetry>> DependencyMetrics => this.dependencyTelemetryMetrics.Values;

        public IEnumerable<OperationalizedMetric<ExceptionTelemetry>> ExceptionMetrics => this.exceptionTelemetryMetrics.Values;

        public IEnumerable<OperationalizedMetric<EventTelemetry>> EventMetrics => this.eventTelemetryMetrics.Values;

        public IEnumerable<HashSet<Tuple<string, string>>> AllMetricIds => this.allMetricIds;

        public CollectionConfiguration(CollectionConfigurationInfo info, out string[] errors)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            this.info = info;

            var errorList = new List<string>();

            foreach (OperationalizedMetricInfo metricInfo in this.info.Metrics)
            {
                switch (metricInfo.TelemetryType)
                {
                    case TelemetryType.Request:
                        CollectionConfiguration.MergeMetric(metricInfo, this.requestTelemetryMetrics, out errors);
                        break;
                    case TelemetryType.Dependency:
                        CollectionConfiguration.MergeMetric(metricInfo, this.dependencyTelemetryMetrics, out errors);
                        break;
                    case TelemetryType.Exception:
                        CollectionConfiguration.MergeMetric(metricInfo, this.exceptionTelemetryMetrics, out errors);
                        break;
                    case TelemetryType.Event:
                        CollectionConfiguration.MergeMetric(metricInfo, this.eventTelemetryMetrics, out errors);
                        break;
                    default:
                        errorList.Add(string.Format(CultureInfo.InvariantCulture, "TelemetryType is not supported: {0}", metricInfo.TelemetryType));
                        break;
                }
            }

            errors = errorList.ToArray();

            foreach (var metricIds in
                this.requestTelemetryMetrics.Values.Select(metric => metric.IdsToReportUnder)
                    .Concat(this.dependencyTelemetryMetrics.Values.Select(metric => metric.IdsToReportUnder))
                    .Concat(this.exceptionTelemetryMetrics.Values.Select(metric => metric.IdsToReportUnder))
                    .Concat(this.eventTelemetryMetrics.Values.Select(metric => metric.IdsToReportUnder)))
            {
                this.allMetricIds.Add(metricIds);
            }
        }

        public AggregationType GetAggregationType(Tuple<string, string> metricIds)
        {
            try
            {
                return this.info.Metrics.Single(metric => Tuple.Create(metric.Id, metric.SessionId).Equals(metricIds)).Aggregation;
            }
            catch (Exception)
            {
                // couldn't find the metric for some reason, this is unexpected
                QuickPulseEventSource.Log.UnknownErrorEvent("Unable to find a metric for the accumulator's id.");

                throw;
            }
        }

        private static void MergeMetric<TTelemetry>(
            OperationalizedMetricInfo metricInfo,
            Dictionary<string, OperationalizedMetric<TTelemetry>> metrics,
            out string[] errors)
        {
            errors = new string[] { };

            OperationalizedMetric<TTelemetry> existingEquivalentRequestMetric;
            if (metrics.TryGetValue(metricInfo.ToString(), out existingEquivalentRequestMetric))
            {
                // an equivalent metric already exists, update it
                existingEquivalentRequestMetric.IdsToReportUnder.Add(Tuple.Create(metricInfo.Id, metricInfo.SessionId));
            }
            else
            {
                // no equivalent metrics exist
                metrics.Add(metricInfo.ToString(), new OperationalizedMetric<TTelemetry>(metricInfo, out errors));
            }
        }
    }
}