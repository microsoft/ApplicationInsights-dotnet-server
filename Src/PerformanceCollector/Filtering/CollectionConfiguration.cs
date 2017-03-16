namespace Microsoft.ApplicationInsights.Extensibility.Filtering
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.Implementation.QuickPulse.Helpers;

    internal class CollectionConfiguration
    {
        private readonly CollectionConfigurationInfo info;

        private readonly List<OperationalizedMetric<RequestTelemetry>> requestTelemetryMetrics = new List<OperationalizedMetric<RequestTelemetry>>();

        private readonly List<OperationalizedMetric<DependencyTelemetry>> dependencyTelemetryMetrics =
            new List<OperationalizedMetric<DependencyTelemetry>>();

        private readonly List<OperationalizedMetric<ExceptionTelemetry>> exceptionTelemetryMetrics =
            new List<OperationalizedMetric<ExceptionTelemetry>>();

        private readonly List<OperationalizedMetric<EventTelemetry>> eventTelemetryMetrics = new List<OperationalizedMetric<EventTelemetry>>();

        private readonly List<OperationalizedMetric<TraceTelemetry>> traceTelemetryMetrics = new List<OperationalizedMetric<TraceTelemetry>>();

        private readonly List<OperationalizedMetric<MetricValue>> metricMetrics = new List<OperationalizedMetric<MetricValue>>();

        private readonly List<Tuple<string, AggregationType>> telemetryMetadata = new List<Tuple<string, AggregationType>>();

        private readonly List<Tuple<string, AggregationType>> metricMetadata = new List<Tuple<string, AggregationType>>();

        private readonly List<DocumentStream> documentStreams = new List<DocumentStream>(); 

        public IEnumerable<OperationalizedMetric<RequestTelemetry>> RequestMetrics => this.requestTelemetryMetrics;

        public IEnumerable<OperationalizedMetric<DependencyTelemetry>> DependencyMetrics => this.dependencyTelemetryMetrics;

        public IEnumerable<OperationalizedMetric<ExceptionTelemetry>> ExceptionMetrics => this.exceptionTelemetryMetrics;

        public IEnumerable<OperationalizedMetric<EventTelemetry>> EventMetrics => this.eventTelemetryMetrics;

        public IEnumerable<OperationalizedMetric<TraceTelemetry>> TraceMetrics => this.traceTelemetryMetrics;

        public IEnumerable<OperationalizedMetric<MetricValue>> MetricMetrics => this.metricMetrics;

        /// <summary>
        /// Telemetry types only (handled by QuickPulseTelemetryProcessor)
        /// </summary>
        public IEnumerable<Tuple<string, AggregationType>> TelemetryMetadata => this.telemetryMetadata;

        /// <summary>
        /// Metric type only (handled by QuickPulseMetricProcessor)
        /// </summary>
        public IEnumerable<Tuple<string, AggregationType>> MetricMetadata => this.metricMetadata;

        /// <summary>
        /// Document streams (handled by QuickPulseTelemetryProcessor)
        /// </summary>
        public IEnumerable<DocumentStream> DocumentStreams => this.documentStreams; 
        
        public string ETag => this.info.ETag;

        /// <remarks>
        ///  Performance counter name is stored in OperationalizedMetricInfo.Projection
        /// </remarks>
        public IEnumerable<string> PerformanceCounters
        {
            get
            {
                return
                    (this.info.Metrics ?? new OperationalizedMetricInfo[0]).Where(metric => metric.TelemetryType == TelemetryType.PerformanceCounter)
                        .Select(metric => metric.Projection);
            }
        }

        public CollectionConfiguration(
            CollectionConfigurationInfo info,
            out string[] errors,
            Clock timeProvider,
            IEnumerable<DocumentStream> previousDocumentStreams = null)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            this.info = info;

            // create metrics based on descriptions in info
            string[] metricErrors;
            this.CreateMetrics(info, out metricErrors);

            // maintain a separate collection of all (Id, AggregationType) pairs with some additional data - to allow for uniform access to all types of metrics
            this.CreateMetadata();

            // create document streams based on description in info
            string[] documentStreamErrors;
            this.CreateDocumentStreams(out documentStreamErrors, timeProvider, previousDocumentStreams ?? new DocumentStream[0]);

            errors = metricErrors.Concat(documentStreamErrors).ToArray();
        }

        private void CreateDocumentStreams(out string[] errors, Clock timeProvider, IEnumerable<DocumentStream> previousDocumentStreams)
        {
            var errorList = new List<string>();
            var documentStreamIds = new HashSet<string>();

            // quota might be changing concurrently on the collection thread, but we don't need the exact value at any given time
            // we will try to carry over the last known values to this new configuration
            Dictionary<string, Tuple<float, float, float, float, float>> previousQuotasByStreamId =
                previousDocumentStreams.ToDictionary(
                    documentStream => documentStream.Id,
                    documentStream =>
                    Tuple.Create(
                        documentStream.RequestQuotaTracker.CurrentQuota,
                        documentStream.DependencyQuotaTracker.CurrentQuota,
                        documentStream.ExceptionQuotaTracker.CurrentQuota,
                        documentStream.EventQuotaTracker.CurrentQuota,
                        documentStream.TraceQuotaTracker.CurrentQuota));

            foreach (DocumentStreamInfo documentStreamInfo in info.DocumentStreams ?? new DocumentStreamInfo[0])
            {
                if (documentStreamIds.Contains(documentStreamInfo.Id))
                {
                    // there must not be streams with duplicate ids
                    errorList.Add(
                        string.Format(CultureInfo.InvariantCulture, "Document stream with a duplicate id ignored: {0}", documentStreamInfo.Id));

                    continue;
                }

                try
                {
                    Tuple<float, float, float, float, float> initialQuotas;
                    previousQuotasByStreamId.TryGetValue(documentStreamInfo.Id, out initialQuotas);

                    string[] localErrors;
                    var documentStream = new DocumentStream(
                        documentStreamInfo,
                        out localErrors,
                        timeProvider,
                        initialQuotas?.Item1,
                        initialQuotas?.Item2,
                        initialQuotas?.Item3,
                        initialQuotas?.Item4,
                        initialQuotas?.Item5);
                    
                    errorList.AddRange(localErrors ?? new string[0]);
                    documentStreamIds.Add(documentStreamInfo.Id);

                    this.documentStreams.Add(documentStream);
                }
                catch (Exception e)
                {
                    errorList.Add(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            "Failed to create document stream {0}. Error message: {1}",
                            documentStreamInfo.ToString(),
                            e.ToString()));
                }
            }

            errors = errorList.ToArray();
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
                    case TelemetryType.PerformanceCounter:
                        // no need to create a wrapper, we rely on the underlying CollectionConfigurationInfo to provide data about performance counters
                        // move on to the next metric
                        continue;
                        break;
                    case TelemetryType.Trace:
                        CollectionConfiguration.AddMetric(metricInfo, this.traceTelemetryMetrics, out localErrors);
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
                    .Concat(this.eventTelemetryMetrics.Select(metric => Tuple.Create(metric.Id, metric.AggregationType)))
                    .Concat(this.traceTelemetryMetrics.Select(metric => Tuple.Create(metric.Id, metric.AggregationType))))
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