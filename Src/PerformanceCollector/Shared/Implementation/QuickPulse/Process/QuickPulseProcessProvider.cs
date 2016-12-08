namespace Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.Implementation.QuickPulse
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.Implementation.QuickPulse.PerfLib;

    /// <summary>
    /// Top CPU collector.
    /// </summary>
    internal sealed class QuickPulseProcessProvider : IQuickPulseProcessProvider
    {
        private readonly IQuickPulsePerfLib perfLib;

        public QuickPulseProcessProvider(IQuickPulsePerfLib perfLib)
        {
            this.perfLib = perfLib ?? PerfLib.PerfLib.GetPerfLib();
        }

        public void Close()
        {
            this.perfLib.Close();
        }

        public IEnumerable<QuickPulseProcess> GetProcesses(out TimeSpan? totalTime)
        {
            try
            {
                const int CategoryIndex = 230;
                const int CounterIndex = 6;
                const string TotalInstanceName = "_Total";
                const string IdleInstanceName = "Idle";

                CategorySample categorySample = this.perfLib.GetCategorySample(CategoryIndex, CounterIndex);
                CounterDefinitionSample counterSample = categorySample.CounterTable[CounterIndex];

                var procValues = new Dictionary<string, long>();
                foreach (var pair in categorySample.InstanceNameTable)
                {
                    string instanceName = pair.Key;
                    int valueIndex = pair.Value;

                    long instanceValue = counterSample.GetInstanceValue(valueIndex);

                    procValues.Add(instanceName, instanceValue);
                }

                long overallTime;
                totalTime = procValues.TryGetValue(TotalInstanceName, out overallTime) ? TimeSpan.FromTicks(overallTime) : (TimeSpan?)null;

                return
                    procValues.Where(
                        pv =>
                        !string.Equals(pv.Key, TotalInstanceName, StringComparison.Ordinal)
                        && !string.Equals(pv.Key, IdleInstanceName, StringComparison.Ordinal))
                        .Select(pv => new QuickPulseProcess(pv.Key, TimeSpan.FromTicks(pv.Value)));
            }
            catch (Exception)
            {
                totalTime = null;

                return new QuickPulseProcess[] { };
            }
        }
    }
}