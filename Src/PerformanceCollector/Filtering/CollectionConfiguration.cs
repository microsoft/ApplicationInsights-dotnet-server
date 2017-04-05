﻿namespace Microsoft.ApplicationInsights.Extensibility.Filtering
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.Implementation.QuickPulse.Helpers;

    /// <summary>
    /// Represents the collection configuration - a customizable description of performance counters, metrics, and full telemetry documents
    /// to be collected by the SDK.
    /// </summary>
    /// <remarks>
    /// This class is a hub for all pieces of configurable collection configuration.
    /// Upon initialization
    ///   - it creates collection-time instances of <see cref="CalculatedMetric&lt;T&gt;"/> and maintains them in separate collections by telemetry type.
    ///     These are used to filter and calculated calculated metrics configured by the service.
    ///   - it creates collection-time instances of <see cref="DocumentStream"/> which are used to filter and send out full telemetry documents.
    ///   - it creates certain metadata collections which are used by other collection-time components to learn more about what is being collected at any given time.
    /// </remarks>
    internal class CollectionConfiguration
    {
        private readonly CollectionConfigurationInfo info;

        #region Collection-time instances used to filter and calculate data on telemetry passing through the pipeline
        private readonly List<CalculatedMetric<RequestTelemetry>> requestTelemetryMetrics = new List<CalculatedMetric<RequestTelemetry>>();

        private readonly List<CalculatedMetric<DependencyTelemetry>> dependencyTelemetryMetrics =
            new List<CalculatedMetric<DependencyTelemetry>>();

        private readonly List<CalculatedMetric<ExceptionTelemetry>> exceptionTelemetryMetrics =
            new List<CalculatedMetric<ExceptionTelemetry>>();

        private readonly List<CalculatedMetric<EventTelemetry>> eventTelemetryMetrics = new List<CalculatedMetric<EventTelemetry>>();

        private readonly List<CalculatedMetric<TraceTelemetry>> traceTelemetryMetrics = new List<CalculatedMetric<TraceTelemetry>>();

        private readonly List<CalculatedMetric<MetricValue>> metricMetrics = new List<CalculatedMetric<MetricValue>>();

        private readonly List<DocumentStream> documentStreams = new List<DocumentStream>();
        #endregion

        #region Metadata used by other components
        private readonly List<Tuple<string, AggregationType>> telemetryMetadata = new List<Tuple<string, AggregationType>>();

        private readonly List<Tuple<string, AggregationType>> metricMetadata = new List<Tuple<string, AggregationType>>();
        
        private readonly List<Tuple<string, string>> performanceCounters = new List<Tuple<string, string>>();
        #endregion

        public CollectionConfiguration(
           CollectionConfigurationInfo info,
           out CollectionConfigurationError[] errors,
           Clock timeProvider,
           IEnumerable<DocumentStream> previousDocumentStreams = null)
        {
            if (info == null)
            {
                throw new ArgumentNullException(nameof(info));
            }

            this.info = info;

            // create metrics based on descriptions in info
            CollectionConfigurationError[] metricErrors;
            this.CreateMetrics(info, out metricErrors);

            // maintain a separate collection of all (Id, AggregationType) pairs with some additional data - to allow for uniform access to all types of metrics
            this.CreateMetadata();

            // create document streams based on description in info
            CollectionConfigurationError[] documentStreamErrors;
            this.CreateDocumentStreams(out documentStreamErrors, timeProvider, previousDocumentStreams ?? new DocumentStream[0]);

            // check performance counters
            CollectionConfigurationError[] performanceCounterErrors;
            this.CreatePerformanceCounters(out performanceCounterErrors);

            errors = metricErrors.Concat(documentStreamErrors).Concat(performanceCounterErrors).ToArray();

            foreach (var error in errors)
            {
                error.Data["ETag"] = this.info.ETag;
            }
        }

        public IEnumerable<CalculatedMetric<RequestTelemetry>> RequestMetrics => this.requestTelemetryMetrics;

        public IEnumerable<CalculatedMetric<DependencyTelemetry>> DependencyMetrics => this.dependencyTelemetryMetrics;

        public IEnumerable<CalculatedMetric<ExceptionTelemetry>> ExceptionMetrics => this.exceptionTelemetryMetrics;

        public IEnumerable<CalculatedMetric<EventTelemetry>> EventMetrics => this.eventTelemetryMetrics;

        public IEnumerable<CalculatedMetric<TraceTelemetry>> TraceMetrics => this.traceTelemetryMetrics;

        public IEnumerable<CalculatedMetric<MetricValue>> MetricMetrics => this.metricMetrics;

        /// <summary>
        /// Telemetry types only (handled by QuickPulseTelemetryProcessor).
        /// </summary>
        public IEnumerable<Tuple<string, AggregationType>> TelemetryMetadata => this.telemetryMetadata;

        /// <summary>
        /// Metric type only (handled by QuickPulseMetricProcessor).
        /// </summary>
        public IEnumerable<Tuple<string, AggregationType>> MetricMetadata => this.metricMetadata;

        /// <summary>
        /// Document streams (handled by QuickPulseTelemetryProcessor).
        /// </summary>
        public IEnumerable<DocumentStream> DocumentStreams => this.documentStreams; 
        
        public string ETag => this.info.ETag;

        /// <summary>
        /// Gets a list of performance counters.
        /// </summary>
        /// <remarks>
        /// Performance counter name is stored in CalculatedMetricInfo.Projection.
        /// </remarks>
        public IEnumerable<Tuple<string, string>> PerformanceCounters => this.performanceCounters;

        private static void AddMetric<TTelemetry>(
          CalculatedMetricInfo metricInfo,
          List<CalculatedMetric<TTelemetry>> metrics,
          out CollectionConfigurationError[] errors)
        {
            errors = new CollectionConfigurationError[] { };

            try
            {
                metrics.Add(new CalculatedMetric<TTelemetry>(metricInfo, out errors));
            }
            catch (Exception e)
            {
                // error creating the metric
                errors =
                    errors.Concat(
                        new[]
                        {
                            CollectionConfigurationError.CreateError(
                                CollectionConfigurationErrorType.MetricFailureToCreate,
                                string.Format(CultureInfo.InvariantCulture, "Failed to create metric {0}.", metricInfo),
                                e,
                                Tuple.Create("MetricId", metricInfo.Id))
                        }).ToArray();
            }
        }

        private void CreatePerformanceCounters(out CollectionConfigurationError[] errors)
        {
            var errorList = new List<CollectionConfigurationError>();

            CalculatedMetricInfo[] performanceCounterMetrics =
                (this.info.Metrics ?? new CalculatedMetricInfo[0]).Where(metric => metric.TelemetryType == TelemetryType.PerformanceCounter)
                    .ToArray();

            this.performanceCounters.AddRange(
                performanceCounterMetrics.GroupBy(metric => metric.Id, StringComparer.Ordinal)
                    .Select(group => group.First())
                    .Select(pc => Tuple.Create(pc.Id, pc.Projection)));

            IEnumerable<string> duplicateMetricIds =
                performanceCounterMetrics.GroupBy(pc => pc.Id, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key);

            foreach (var duplicateMetricId in duplicateMetricIds)
            {
                errorList.Add(
                    CollectionConfigurationError.CreateError(
                        CollectionConfigurationErrorType.PerformanceCounterDuplicateIds,
                        string.Format(CultureInfo.InvariantCulture, "Duplicate performance counter id '{0}'", duplicateMetricId),
                        null,
                        Tuple.Create("MetricId", duplicateMetricId)));
            }

            errors = errorList.ToArray();
        }

        private void CreateDocumentStreams(
            out CollectionConfigurationError[] errors,
            Clock timeProvider,
            IEnumerable<DocumentStream> previousDocumentStreams)
        {
            var errorList = new List<CollectionConfigurationError>();
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

            foreach (DocumentStreamInfo documentStreamInfo in this.info.DocumentStreams ?? new DocumentStreamInfo[0])
            {
                if (documentStreamIds.Contains(documentStreamInfo.Id))
                {
                    // there must not be streams with duplicate ids
                    errorList.Add(
                        CollectionConfigurationError.CreateError(
                            CollectionConfigurationErrorType.DocumentStreamDuplicateIds,
                            string.Format(CultureInfo.InvariantCulture, "Document stream with a duplicate id ignored: {0}", documentStreamInfo.Id),
                            null,
                            Tuple.Create("DocumentStreamId", documentStreamInfo.Id)));

                    continue;
                }

                CollectionConfigurationError[] localErrors = null;
                try
                {
                    Tuple<float, float, float, float, float> initialQuotas;
                    previousQuotasByStreamId.TryGetValue(documentStreamInfo.Id, out initialQuotas);

                    var documentStream = new DocumentStream(
                        documentStreamInfo,
                        out localErrors,
                        timeProvider,
                        initialQuotas?.Item1,
                        initialQuotas?.Item2,
                        initialQuotas?.Item3,
                        initialQuotas?.Item4,
                        initialQuotas?.Item5);

                    documentStreamIds.Add(documentStreamInfo.Id);
                    this.documentStreams.Add(documentStream);
                }
                catch (Exception e)
                {
                    errorList.Add(
                        CollectionConfigurationError.CreateError(
                            CollectionConfigurationErrorType.DocumentStreamFailureToCreate,
                            string.Format(CultureInfo.InvariantCulture, "Failed to create document stream {0}", documentStreamInfo),
                            e,
                            Tuple.Create("DocumentStreamId", documentStreamInfo.Id)));
                }

                if (localErrors != null)
                {
                    foreach (var error in localErrors)
                    {
                        error.Data["DocumentStreamId"] = documentStreamInfo.Id;
                    }

                    errorList.AddRange(localErrors);
                }
            }

            errors = errorList.ToArray();
        }

        private void CreateMetrics(CollectionConfigurationInfo info, out CollectionConfigurationError[] errors)
        {
            var errorList = new List<CollectionConfigurationError>();
            var metricIds = new HashSet<string>();

            foreach (CalculatedMetricInfo metricInfo in info.Metrics ?? new CalculatedMetricInfo[0])
            {
                if (metricIds.Contains(metricInfo.Id))
                {
                    // there must not be metrics with duplicate ids
                    errorList.Add(
                        CollectionConfigurationError.CreateError(
                            CollectionConfigurationErrorType.MetricDuplicateIds,
                            string.Format(CultureInfo.InvariantCulture, "Metric with a duplicate id ignored: {0}", metricInfo.Id),
                            null,
                            Tuple.Create("MetricId", metricInfo.Id)));

                    continue;
                }

                CollectionConfigurationError[] localErrors = null;
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
                    case TelemetryType.Trace:
                        CollectionConfiguration.AddMetric(metricInfo, this.traceTelemetryMetrics, out localErrors);
                        break;
                    default:
                        errorList.Add(
                            CollectionConfigurationError.CreateError(
                                CollectionConfigurationErrorType.MetricTelemetryTypeUnsupported,
                                string.Format(CultureInfo.InvariantCulture, "TelemetryType is not supported: {0}", metricInfo.TelemetryType),
                                null,
                                Tuple.Create("MetricId", metricInfo.Id),
                                Tuple.Create("TelemetryType", metricInfo.TelemetryType.ToString())));
                        break;
                }

                errorList.AddRange(localErrors ?? new CollectionConfigurationError[0]);

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
    }
}