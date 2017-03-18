namespace Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.Implementation.QuickPulse
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Runtime.Serialization.Json;

    using Helpers;

    using Microsoft.ApplicationInsights.Extensibility.Filtering;
    using Microsoft.ApplicationInsights.Extensibility.Implementation.Tracing;
    using Microsoft.ManagementServices.RealTimeDataProcessing.QuickPulseService;

    /// <summary>
    /// Service client for QPS service.
    /// </summary>
    internal sealed class QuickPulseServiceClient : IQuickPulseServiceClient
    {
        private readonly string instanceName;

        private readonly string streamId;

        private readonly string machineName;

        private readonly string version;

        private readonly TimeSpan timeout = TimeSpan.FromSeconds(3);

        private readonly Clock timeProvider;

        private readonly bool isWebApp;

        private readonly DataContractJsonSerializer serializerDataPoint = new DataContractJsonSerializer(typeof(MonitoringDataPoint));

        private readonly DataContractJsonSerializer serializerDataPointArray = new DataContractJsonSerializer(typeof(MonitoringDataPoint[]));

        private readonly DataContractJsonSerializer deserializerServerResponse = new DataContractJsonSerializer(typeof(CollectionConfigurationInfo));

        private readonly HttpClient httpClient;

        public QuickPulseServiceClient(
            Uri serviceUri,
            string instanceName,
            string streamId,
            string machineName,
            string version,
            Clock timeProvider,
            bool isWebApp,
            TimeSpan? timeout = null)
        {
            this.ServiceUri = serviceUri;
            this.instanceName = instanceName;
            this.streamId = streamId;
            this.machineName = machineName;
            this.version = version;
            this.timeProvider = timeProvider;
            this.isWebApp = isWebApp;
            this.timeout = timeout ?? this.timeout;

            this.httpClient = new HttpClient() { Timeout = this.timeout };
        }

        public Uri ServiceUri { get; }

        public bool? Ping(
            string instrumentationKey,
            DateTimeOffset timestamp,
            string configurationETag,
            string authApiKey,
            out CollectionConfigurationInfo configurationInfo)
        {
            var requestUri = string.Format(
                CultureInfo.InvariantCulture,
                "{0}/ping?ikey={1}",
                this.ServiceUri.AbsoluteUri.TrimEnd('/'),
                Uri.EscapeUriString(instrumentationKey));

            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUri))
            {
                using (
                    HttpResponseMessage response = this.SendRequest(
                        request,
                        true,
                        configurationETag,
                        authApiKey,
                        r => this.WritePingData(timestamp, r)))
                {
                    if (response == null)
                    {
                        configurationInfo = null;
                        return null;
                    }

                    return this.ProcessResponse(response, configurationETag, out configurationInfo);
                }
            }
        }

        public bool? SubmitSamples(
            IEnumerable<QuickPulseDataSample> samples,
            string instrumentationKey,
            string configurationETag,
            string authApiKey,
            out CollectionConfigurationInfo configurationInfo,
            CollectionConfigurationError[] collectionConfigurationErrors)
        {
            var requestUri = string.Format(
                CultureInfo.InvariantCulture,
                "{0}/post?ikey={1}",
                this.ServiceUri.AbsoluteUri.TrimEnd('/'),
                Uri.EscapeUriString(instrumentationKey));

            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestUri))
            {
                using (
                    HttpResponseMessage response = this.SendRequest(
                        request,
                        false,
                        configurationETag,
                        authApiKey,
                        r => this.WriteSamples(samples, instrumentationKey, r, collectionConfigurationErrors)))
                {

                    if (response == null)
                    {
                        configurationInfo = null;
                        return null;
                    }

                    return this.ProcessResponse(response, configurationETag, out configurationInfo);
                }
            }
        }

        private bool? ProcessResponse(HttpResponseMessage response, string configurationETag, out CollectionConfigurationInfo configurationInfo)
        {
            configurationInfo = null;

            Dictionary<string, string> headers = response.Headers.Concat(response.Content.Headers)
                .ToDictionary(pair => pair.Key, pair => pair.Value.FirstOrDefault());

            string subscribedHeaderValue;
            headers.TryGetValue(QuickPulseConstants.XMsQpsSubscribedHeaderName, out subscribedHeaderValue);

            bool isSubscribed;
            if (!bool.TryParse(subscribedHeaderValue, out isSubscribed))
            {
                // read the response out to avoid issues with TCP connections not being freed up
                try
                {
                    var responseBytes = response.Content.ReadAsByteArrayAsync().Result;
                }
                catch (Exception)
                {
                    // we did our best, we don't care about the outcome anyway
                }

                return null;
            }

            string configurationETagHeaderValue;
            headers.TryGetValue(QuickPulseConstants.XMsQpsConfigurationETagHeaderName, out configurationETagHeaderValue);

            try
            {
                using (Stream responseStream = response.Content.ReadAsStreamAsync().Result)
                {
                    if (isSubscribed && !string.IsNullOrEmpty(configurationETagHeaderValue)
                        && !string.Equals(configurationETagHeaderValue, configurationETag, StringComparison.Ordinal))
                    {
                        configurationInfo = this.deserializerServerResponse.ReadObject(responseStream) as CollectionConfigurationInfo;
                    }
                }
            }
            catch (Exception e)
            {
                // couldn't read or deserialize the response
                QuickPulseEventSource.Log.ServiceCommunicationFailedEvent(e.ToInvariantString());
            }

            return isSubscribed;
        }

        private static double Round(double value)
        {
            return Math.Round(value, 4, MidpointRounding.AwayFromZero);
        }

        private void WritePingData(DateTimeOffset timestamp, HttpRequestMessage request)
        {
            var dataPoint = new MonitoringDataPoint
            {
                Version = this.version,
                InvariantVersion = MonitoringDataPoint.CurrentInvariantVersion,
                //InstrumentationKey = instrumentationKey, // ikey is currently set in query string parameter
                Instance = this.instanceName,
                StreamId = this.streamId,
                MachineName = this.machineName,
                Timestamp = timestamp.UtcDateTime,
                IsWebApp = this.isWebApp
            };

            var ms = new MemoryStream();
            this.serializerDataPoint.WriteObject(ms, dataPoint);

            ms.Position = 0;
            request.Content = new StreamContent(ms);
        }

        private void WriteSamples(IEnumerable<QuickPulseDataSample> samples, string instrumentationKey, HttpRequestMessage request, CollectionConfigurationError[] errors)
        {
            var monitoringPoints = new List<MonitoringDataPoint>();

            foreach (var sample in samples)
            {
                var metricPoints = new List<MetricPoint>();

                metricPoints.AddRange(CreateDefaultMetrics(sample));

                metricPoints.AddRange(
                    sample.PerfCountersLookup.Select(counter => new MetricPoint { Name = counter.Key, Value = Round(counter.Value), Weight = 1 }));

                metricPoints.AddRange(CreateOperationalizedMetrics(sample));

                ITelemetryDocument[] documents = sample.TelemetryDocuments.ToArray();
                Array.Reverse(documents);

                ProcessCpuData[] topCpuProcesses =
                    sample.TopCpuData.Select(p => new ProcessCpuData() { ProcessName = p.Item1, CpuPercentage = p.Item2 }).ToArray();

                var dataPoint = new MonitoringDataPoint
                {
                    Version = this.version,
                    InvariantVersion = MonitoringDataPoint.CurrentInvariantVersion,
                    InstrumentationKey = instrumentationKey,
                    Instance = this.instanceName,
                    StreamId = this.streamId,
                    MachineName = this.machineName,
                    Timestamp = sample.EndTimestamp.UtcDateTime,
                    IsWebApp = this.isWebApp,
                    Metrics = metricPoints.ToArray(),
                    Documents = documents,
                    TopCpuProcesses = topCpuProcesses.Length > 0 ? topCpuProcesses : null,
                    TopCpuDataAccessDenied = sample.TopCpuDataAccessDenied,
                    CollectionConfigurationErrors = errors
                };

                monitoringPoints.Add(dataPoint);
            }

            var ms = new MemoryStream();
            this.serializerDataPointArray.WriteObject(ms, monitoringPoints.ToArray());

            ms.Position = 0;
            request.Content = new StreamContent(ms);
        }

        private static IEnumerable<MetricPoint> CreateOperationalizedMetrics(QuickPulseDataSample sample)
        {
            var metrics = new List<MetricPoint>();

            foreach (AccumulatedValue metricAccumulatedValue in
                sample.CollectionConfigurationAccumulator.MetricAccumulators.Values)
            {
                try
                {
                    double[] accumulatedValues = metricAccumulatedValue.Value.ToArray();

                    MetricPoint metricPoint = new MetricPoint
                    {
                        Name = metricAccumulatedValue.MetricId,
                        Value = OperationalizedMetric<int>.Aggregate(accumulatedValues, metricAccumulatedValue.AggregationType),
                        Weight = accumulatedValues.Length
                    };
                    metrics.Add(metricPoint);
                }
                catch (Exception e)
                {
                    // skip this metric
                    QuickPulseEventSource.Log.UnknownErrorEvent(e.ToString());
                }
            }

            return metrics;
        }

        private static IEnumerable<MetricPoint> CreateDefaultMetrics(QuickPulseDataSample sample)
        {
            return new[]
            {
                new MetricPoint { Name = @"\ApplicationInsights\Requests/Sec", Value = Round(sample.AIRequestsPerSecond), Weight = 1 },
                new MetricPoint
                {
                    Name = @"\ApplicationInsights\Request Duration",
                    Value = Round(sample.AIRequestDurationAveInMs),
                    Weight = sample.AIRequests
                },
                new MetricPoint { Name = @"\ApplicationInsights\Requests Failed/Sec", Value = Round(sample.AIRequestsFailedPerSecond), Weight = 1 },
                new MetricPoint
                {
                    Name = @"\ApplicationInsights\Requests Succeeded/Sec",
                    Value = Round(sample.AIRequestsSucceededPerSecond),
                    Weight = 1
                },
                new MetricPoint { Name = @"\ApplicationInsights\Dependency Calls/Sec", Value = Round(sample.AIDependencyCallsPerSecond), Weight = 1 },
                new MetricPoint
                {
                    Name = @"\ApplicationInsights\Dependency Call Duration",
                    Value = Round(sample.AIDependencyCallDurationAveInMs),
                    Weight = sample.AIDependencyCalls
                },
                new MetricPoint
                {
                    Name = @"\ApplicationInsights\Dependency Calls Failed/Sec",
                    Value = Round(sample.AIDependencyCallsFailedPerSecond),
                    Weight = 1
                },
                new MetricPoint
                {
                    Name = @"\ApplicationInsights\Dependency Calls Succeeded/Sec",
                    Value = Round(sample.AIDependencyCallsSucceededPerSecond),
                    Weight = 1
                },
                new MetricPoint { Name = @"\ApplicationInsights\Exceptions/Sec", Value = Round(sample.AIExceptionsPerSecond), Weight = 1 }
            };
        }

        private HttpResponseMessage SendRequest(
            HttpRequestMessage request,
            bool includeHeaders,
            string configurationETag,
            string authApiKey,
            Action<HttpRequestMessage> onWriteBody)
        {
            try
            {
                request.Headers.Add(
                    QuickPulseConstants.XMsQpsTransmissionTimeHeaderName,
                    this.timeProvider.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
                request.Headers.Add(QuickPulseConstants.XMsQpsConfigurationETagHeaderName, configurationETag);
                request.Headers.Add(QuickPulseConstants.XMsQpsAuthApiKeyHeaderName, authApiKey ?? string.Empty);

                if (includeHeaders)
                {
                    request.Headers.Add(QuickPulseConstants.XMsQpsInstanceNameHeaderName, this.instanceName);
                    request.Headers.Add(QuickPulseConstants.XMsQpsStreamIdHeaderName, this.streamId);
                    request.Headers.Add(QuickPulseConstants.XMsQpsMachineNameHeaderName, this.machineName);
                }

                onWriteBody?.Invoke(request);

                HttpResponseMessage response = this.httpClient.SendAsync(request).Result;

                if (!response.IsSuccessStatusCode)
                {
                    throw new WebException(
                        string.Format(CultureInfo.InvariantCulture, "Unable to contact the server. Response code: {0}", (int)response.StatusCode));
                }

                return response;
            }
            catch (Exception e)
            {
                QuickPulseEventSource.Log.ServiceCommunicationFailedEvent(e.ToInvariantString());
            }

            return null;
        }
    }
}