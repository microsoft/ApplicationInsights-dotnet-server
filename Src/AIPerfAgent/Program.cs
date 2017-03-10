using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector;
using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AIPerfAgent
{
    class Program
    {
        static void Main(string[] args)
        {
            PerfCollectorViaNugetReference();
            QuickPulseViaNugetReference();
            Console.ReadLine();
        }

        private static void QuickPulseViaNugetReference()
        {
            TelemetryConfiguration configuration = new TelemetryConfiguration();
            configuration.InstrumentationKey = "2ef2a25f-73c6-4cf6-99bd-ba7cfe13d3c1";

            QuickPulseTelemetryProcessor processor = null;

            configuration.TelemetryProcessorChainBuilder
                .Use((next) =>
                {
                    processor = new QuickPulseTelemetryProcessor(next);
                    return processor;
                })
                .Build();

            var QuickPulse = new QuickPulseTelemetryModule();
            QuickPulse.Initialize(configuration);
            QuickPulse.RegisterTelemetryProcessor(processor);

            Console.WriteLine("Started QP..");
            //Console.ReadLine();

        }

        private static void PerfCollectorViaNugetReference()
        {
            TelemetryConfiguration configuration = new TelemetryConfiguration();
            configuration.InstrumentationKey = "2ef2a25f-73c6-4cf6-99bd-ba7cfe13d3c1";

            PerformanceCollectorModule perf = new PerformanceCollectorModule();
            perf.Initialize(configuration);

            Console.WriteLine("Started PC..");
            //Console.ReadLine();
              

        }

        private static void PerfCollectorViaSourceCopy()
        {
            StandardPerformanceCollector collector = new StandardPerformanceCollector();
            TelemetryClient client = new TelemetryClient();
            client.InstrumentationKey = "2ef2a25f-73c6-4cf6-99bd-ba7cfe13d3c1";

            List<PerformanceCounterCollectionRequest> defaultCounters = new List<PerformanceCounterCollectionRequest>();
            defaultCounters.Add(new PerformanceCounterCollectionRequest(@"\Process(??APP_WIN32_PROC??)\% Processor Time", @"\Process(??APP_WIN32_PROC??)\% Processor Time"));
            defaultCounters.Add(new PerformanceCounterCollectionRequest(@"\Memory\Available Bytes", @"\Memory\Available Bytes"));

            string error;
            var errors = new List<string>();
            foreach (PerformanceCounterCollectionRequest req in defaultCounters)
            {
                collector.RegisterCounter(
                        req.PerformanceCounter,
                        req.ReportAs,
                        true,
                        out error,
                        false);
                if (!string.IsNullOrWhiteSpace(error))
                {
                    errors.Add(
                        string.Format(
                            CultureInfo.InvariantCulture,
                            //Resources.PerformanceCounterCheckConfigurationEntry,
                            "Fail",
                            req.PerformanceCounter,
                            error));
                }
            }

            while (true)
            {
                var results =
                       collector.Collect(
                           (counterName, e) =>
                           PerformanceCollectorEventSource.Log.CounterReadingFailedEvent(e.ToString(), counterName))
                           .ToList();

                foreach (var result in results)
                {
                    var telemetry = CreateTelemetry(result.Item1, result.Item2);
                    try
                    {
                        client.Track(telemetry);
                    }
                    catch (InvalidOperationException e)
                    {
                        PerformanceCollectorEventSource.Log.TelemetrySendFailedEvent(e.ToString());
                    }
                }

                Thread.Sleep(10000);
            }
        }

        private static MetricTelemetry CreateTelemetry(PerformanceCounterData pc, double value)
        {
            var metricName = !string.IsNullOrWhiteSpace(pc.ReportAs)
                                 ? pc.ReportAs
                                 : string.Format(
                                     CultureInfo.InvariantCulture,
                                     "{0} - {1}",
                                     pc.CategoryName,
                                     pc.CounterName);

            var metricTelemetry = new MetricTelemetry()
            {
                Name = metricName,
                Count = 1,
                Sum = value,
                Min = value,
                Max = value,
                StandardDeviation = 0
            };

            metricTelemetry.Properties.Add("CounterInstanceName", pc.InstanceName);
            metricTelemetry.Properties.Add("CustomPerfCounter", "true");

            return metricTelemetry;
        }

    }
}
