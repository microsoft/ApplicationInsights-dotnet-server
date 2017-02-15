namespace Microsoft.ApplicationInsights.Extensibility.Filtering
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using Microsoft.ApplicationInsights.DataContracts;

    internal class CollectionConfiguration
    {
        private readonly CollectionConfigurationInfo info;

        private readonly Dictionary<string, OperationalizedMetric<RequestTelemetry>> requestTelemetryMetrics =
            new Dictionary<string, OperationalizedMetric<RequestTelemetry>>();

        private readonly Dictionary<string, OperationalizedMetric<DependencyTelemetry>> dependencyTelemetryMetrics =
            new Dictionary<string, OperationalizedMetric<DependencyTelemetry>>();

        private readonly Dictionary<string, OperationalizedMetric<ExceptionTelemetry>> exceptionTelemetryMetrics =
            new Dictionary<string, OperationalizedMetric<ExceptionTelemetry>>();

        private readonly Dictionary<string, OperationalizedMetric<EventTelemetry>> eventTelemetryMetrics =
            new Dictionary<string, OperationalizedMetric<EventTelemetry>>();

        private readonly Dictionary<string, OperationalizedMetric<MetricValue>> metricMetrics =
            new Dictionary<string, OperationalizedMetric<MetricValue>>();

        private readonly List<Tuple<MetricIdCollection, AggregationType>> telemetryMetadata = new List<Tuple<MetricIdCollection, AggregationType>>();

        private readonly List<Tuple<MetricIdCollection, AggregationType>> metricMetadata = new List<Tuple<MetricIdCollection, AggregationType>>();

        public IEnumerable<OperationalizedMetric<RequestTelemetry>> RequestMetrics => this.requestTelemetryMetrics.Values;

        public IEnumerable<OperationalizedMetric<DependencyTelemetry>> DependencyMetrics => this.dependencyTelemetryMetrics.Values;

        public IEnumerable<OperationalizedMetric<ExceptionTelemetry>> ExceptionMetrics => this.exceptionTelemetryMetrics.Values;

        public IEnumerable<OperationalizedMetric<EventTelemetry>> EventMetrics => this.eventTelemetryMetrics.Values;

        public IEnumerable<OperationalizedMetric<MetricValue>> MetricMetrics => this.metricMetrics.Values;

        /// <summary>
        /// Telemetry types only (handled by QuickPulseTelemetryProcessor)
        /// </summary>
        public IEnumerable<Tuple<MetricIdCollection, AggregationType>> TelemetryMetadata => this.telemetryMetadata;

        /// <summary>
        /// Metric type only (handled by QuickPulseMetricProcessor)
        /// </summary>
        public IEnumerable<Tuple<MetricIdCollection, AggregationType>> MetricMetadata => this.metricMetadata;

        public string ETag => this.info.ETag;

        public CollectionConfiguration(CollectionConfigurationInfo info, out string[] errors)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            this.info = info;

            // create metrics based on descriptions in info
            this.CreateMetrics(info, out errors);

            // maintain a separate collection of all (SessionId, Id) pairs with some additional data - to allow for uniform access to all types of metrics
            this.CreateMetadata();
        }

        private void CreateMetadata()
        {
            foreach (var metricIds in
                this.requestTelemetryMetrics.Values.Select(metric => Tuple.Create(metric.IdsToReportUnder, metric.AggregationType))
                    .Concat(this.dependencyTelemetryMetrics.Values.Select(metric => Tuple.Create(metric.IdsToReportUnder, metric.AggregationType)))
                    .Concat(this.exceptionTelemetryMetrics.Values.Select(metric => Tuple.Create(metric.IdsToReportUnder, metric.AggregationType)))
                    .Concat(this.eventTelemetryMetrics.Values.Select(metric => Tuple.Create(metric.IdsToReportUnder, metric.AggregationType))))
            {
                this.telemetryMetadata.Add(metricIds);
            }

            foreach (var metricIds in this.metricMetrics.Values.Select(metric => Tuple.Create(metric.IdsToReportUnder, metric.AggregationType)))
            {
                this.metricMetadata.Add(metricIds);
            }
        }

        private void CreateMetrics(CollectionConfigurationInfo info, out string[] errors)
        {
            var errorList = new List<string>();

            foreach (OperationalizedMetricInfo metricInfo in info.Metrics ?? new OperationalizedMetricInfo[0])
            {
                string[] localErrors = null;
                switch (metricInfo.TelemetryType)
                {
                    case TelemetryType.Request:
                        CollectionConfiguration.MergeMetric(metricInfo, this.requestTelemetryMetrics, out localErrors);
                        break;
                    case TelemetryType.Dependency:
                        CollectionConfiguration.MergeMetric(metricInfo, this.dependencyTelemetryMetrics, out localErrors);
                        break;
                    case TelemetryType.Exception:
                        CollectionConfiguration.MergeMetric(metricInfo, this.exceptionTelemetryMetrics, out localErrors);
                        break;
                    case TelemetryType.Event:
                        CollectionConfiguration.MergeMetric(metricInfo, this.eventTelemetryMetrics, out localErrors);
                        break;
                    case TelemetryType.Metric:
                        CollectionConfiguration.MergeMetric(metricInfo, this.metricMetrics, out localErrors);
                        break;
                    default:
                        errorList.Add(string.Format(CultureInfo.InvariantCulture, "TelemetryType is not supported: {0}", metricInfo.TelemetryType));
                        break;
                }

                errorList.AddRange(localErrors ?? new string[0]);
            }

            errors = errorList.ToArray();
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
                existingEquivalentRequestMetric.IdsToReportUnder.Add(Tuple.Create(metricInfo.SessionId, metricInfo.Id));
            }
            else
            {
                // no equivalent metrics exist
                try
                {
                    metrics.Add(metricInfo.ToString(), new OperationalizedMetric<TTelemetry>(metricInfo, out errors));
                }
                catch (Exception e)
                {
                    // error creating the metric
                    errors =
                        errors.Concat(
                            new[]
                                {
                                    string.Format(
                                        CultureInfo.InvariantCulture,
                                        "Failed to create metric {0}. Error message: {1}",
                                        metricInfo.ToString(),
                                        e.ToString())
                                }).ToArray();
                }
            }
        }
    }
}