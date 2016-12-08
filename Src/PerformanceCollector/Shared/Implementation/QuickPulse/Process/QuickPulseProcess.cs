namespace Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.Implementation.QuickPulse
{
    using System;

    /// <summary>
    /// Top CPU collector.
    /// </summary>
    internal sealed class QuickPulseProcess
    {
        public QuickPulseProcess(string processName, TimeSpan totalProcessorTime)
        {
            this.ProcessName = processName;
            this.TotalProcessorTime = totalProcessorTime;
        }
        
        public string ProcessName { get; }

        public TimeSpan TotalProcessorTime { get; }
    }
}