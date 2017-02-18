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

        private readonly List<OperationalizedMetric<RequestTelemetry>> requestTelemetryMetrics = new List<OperationalizedMetric<RequestTelemetry>>();

        private readonly List<OperationalizedMetric<DependencyTelemetry>> dependencyTelemetryMetrics =
            new List<OperationalizedMetric<DependencyTelemetry>>();

        private readonly List<OperationalizedMetric<ExceptionTelemetry>> exceptionTelemetryMetrics =
            new List<OperationalizedMetric<ExceptionTelemetry>>();

        private readonly List<OperationalizedMetric<EventTelemetry>> eventTelemetryMetrics = new List<OperationalizedMetric<EventTelemetry>>();

        private readonly List<OperationalizedMetric<MetricValue>> metricMetrics = new List<OperationalizedMetric<MetricValue>>();

        private readonly List<Tuple<string, AggregationType>> telemetryMetadata = new List<Tuple<string, AggregationType>>();

        private readonly List<Tuple<string, AggregationType>> metricMetadata = new List<Tuple<string, AggregationType>>();

        public IEnumerable<OperationalizedMetric<RequestTelemetry>> RequestMetrics => this.requestTelemetryMetrics;

        public IEnumerable<OperationalizedMetric<DependencyTelemetry>> DependencyMetrics => this.dependencyTelemetryMetrics;

        public IEnumerable<OperationalizedMetric<ExceptionTelemetry>> ExceptionMetrics => this.exceptionTelemetryMetrics;

        public IEnumerable<OperationalizedMetric<EventTelemetry>> EventMetrics => this.eventTelemetryMetrics;

        public IEnumerable<OperationalizedMetric<MetricValue>> MetricMetrics => this.metricMetrics;

        /// <summary>
        /// Telemetry types only (handled by QuickPulseTelemetryProcessor)
        /// </summary>
        public IEnumerable<Tuple<string, AggregationType>> TelemetryMetadata => this.telemetryMetadata;

        /// <summary>
        /// Metric type only (handled by QuickPulseMetricProcessor)
        /// </summary>
        public IEnumerable<Tuple<string, AggregationType>> MetricMetadata => this.metricMetadata;

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

            // maintain a separate collection of all (Id, AggregationType) pairs with some additional data - to allow for uniform access to all types of metrics
            this.CreateMetadata();
        }

        private void CreateMetrics(CollectionConfigurationInfo info, out string[] errors)
        {
            var errorList = new List<string>();
            var metricIds = new HashSet<string>();

            foreach (OperationalizedMetricInfo metricInfo in info.Metrics ?? new OperationalizedMetricInfo[0])
            {
                if (metricIds.Contains(metricInfo.Id))
                {
                    // there must not be metrics with duplicate ids
                    errorList.Add(string.Format(CultureInfo.InvariantCulture, "Metric with a duplicate id ignored: {0}", metricInfo.Id));
                    continue;
                }

                string[] localErrors = null;
                switch (metricInfo.TelemetryType)
                {
                    case TelemetryType.Request:
                        CollectionConfiguration.AddMetric(metricInfo, this.requestTelemetryMetrics, out localErrors);
                        break;
                    case TelemetryType.Dependency:
                        CollectionConfiguration.AddMetric(metricInfo, this.dependencyTelemetryMetrics, out localErrors);
                        break;
                    case TelemetryType.Exception:
                        CollectionConfiguration.AddMetric(metricInfo, this.exceptionTelemetryMetrics, out localErrors);
                        break;
                    case TelemetryType.Event:
                        CollectionConfiguration.AddMetric(metricInfo, this.eventTelemetryMetrics, out localErrors);
                        break;
                    case TelemetryType.Metric:
                        CollectionConfiguration.AddMetric(metricInfo, this.metricMetrics, out localErrors);
                        break;
                    default:
                        errorList.Add(string.Format(CultureInfo.InvariantCulture, "TelemetryType is not supported: {0}", metricInfo.TelemetryType));
                        break;
                }

                errorList.AddRange(localErrors ?? new string[0]);

                metricIds.Add(metricInfo.Id);
            }

            errors = errorList.ToArray();
        }

        private void CreateMetadata()
        {
            foreach (var metricIds in
                this.requestTelemetryMetrics.Select(metric => Tuple.Create(metric.Id, metric.AggregationType))
                    .Concat(this.dependencyTelemetryMetrics.Select(metric => Tuple.Create(metric.Id, metric.AggregationType)))
                    .Concat(this.exceptionTelemetryMetrics.Select(metric => Tuple.Create(metric.Id, metric.AggregationType)))
                    .Concat(this.eventTelemetryMetrics.Select(metric => Tuple.Create(metric.Id, metric.AggregationType))))
            {
                this.telemetryMetadata.Add(metricIds);
            }

            foreach (var metricIds in this.metricMetrics.Select(metric => Tuple.Create(metric.Id, metric.AggregationType)))
            {
                this.metricMetadata.Add(metricIds);
            }
        }

        private static void AddMetric<TTelemetry>(
            OperationalizedMetricInfo metricInfo,
            List<OperationalizedMetric<TTelemetry>> metrics,
            out string[] errors)
        {
            errors = new string[] { };

            try
            {
                metrics.Add(new OperationalizedMetric<TTelemetry>(metricInfo, out errors));
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