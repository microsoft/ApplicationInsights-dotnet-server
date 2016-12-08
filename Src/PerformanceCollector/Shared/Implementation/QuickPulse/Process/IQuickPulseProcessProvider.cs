namespace Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.Implementation.QuickPulse
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Provider interface for Windows processes.
    /// </summary>
    internal interface IQuickPulseProcessProvider
    {
        void Close();

        IEnumerable<QuickPulseProcess> GetProcesses(out TimeSpan? totalTime);
    }
}