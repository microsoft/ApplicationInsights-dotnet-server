namespace Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.Implementation.QuickPulse
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.Implementation.QuickPulse.Helpers;

    /// <summary>
    /// Top CPU collector.
    /// </summary>
    internal sealed class QuickPulseTopCpuCollector : IQuickPulseTopCpuCollector
    {
        private readonly Clock timeProvider;

        private readonly IQuickPulseProcessProvider processProvider;

        // process name => (last observation timestamp, last observation value)
        private readonly Dictionary<string, TimeSpan> processObservations = new Dictionary<string, TimeSpan>(StringComparer.Ordinal);

        private DateTimeOffset prevObservationTime;

        private TimeSpan? prevOverallTime;

        public QuickPulseTopCpuCollector(Clock timeProvider, IQuickPulseProcessProvider processProvider)
        {
            this.timeProvider = timeProvider;
            this.processProvider = processProvider;
        }

        public IEnumerable<Tuple<string, int>> GetTopProcessesByCpu(int topN)
        {
            try
            {
                var procData = new List<Tuple<string, double>>();
                var encounteredProcs = new HashSet<string>();

                DateTimeOffset now = this.timeProvider.UtcNow;

                TimeSpan? totalTime;
                foreach (var process in this.processProvider.GetProcesses(out totalTime))
                {
                    encounteredProcs.Add(process.ProcessName);
                    
                    TimeSpan lastObservation;
                    if (!this.processObservations.TryGetValue(process.ProcessName, out lastObservation))
                    {
                        // this is the first time we're encountering this process
                        this.processObservations.Add(process.ProcessName, process.TotalProcessorTime);

                        continue;
                    }

                    TimeSpan cpuTimeSinceLast = process.TotalProcessorTime - lastObservation;

                    this.processObservations[process.ProcessName] = process.TotalProcessorTime;

                    // use perf data if available; otherwise, calculate it ourselves
                    TimeSpan timeElapsedOnAllCoresSinceLast = (totalTime - this.prevOverallTime)
                                                              ?? TimeSpan.FromTicks((now - this.prevObservationTime).Ticks * Environment.ProcessorCount);

                    double usagePercentage = timeElapsedOnAllCoresSinceLast.Ticks > 0
                                                 ? (double)cpuTimeSinceLast.Ticks / timeElapsedOnAllCoresSinceLast.Ticks
                                                 : 0;

                    procData.Add(Tuple.Create(process.ProcessName, usagePercentage));
                }

                this.CleanState(encounteredProcs);

                this.prevObservationTime = now;
                this.prevOverallTime = totalTime;

                // TODO: implement partial sort instead of full sort below
                return procData.OrderByDescending(p => p.Item2).Select(p => Tuple.Create(p.Item1, (int)(p.Item2 * 100))).Take(topN);
            }
            catch (Exception e)
            {
                QuickPulseEventSource.Log.ProcessesReadingFailedEvent(e.ToString());

                return Enumerable.Empty<Tuple<string, int>>();
            }
        }

        public void Close()
        {
            this.processProvider.Close();
        }

        private void CleanState(HashSet<string> encounteredProcs)
        {
            // remove processes that we haven't encountered this time around
            string[] processCpuKeysToRemove = this.processObservations.Keys.Where(p => !encounteredProcs.Contains(p)).ToArray();
            foreach (var key in processCpuKeysToRemove)
            {
                this.processObservations.Remove(key);
            }
        }
    }
}