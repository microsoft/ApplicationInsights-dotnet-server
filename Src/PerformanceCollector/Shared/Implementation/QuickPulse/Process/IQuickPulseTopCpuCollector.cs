namespace Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.Implementation.QuickPulse
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Interface for top CPU collector.
    /// </summary>
    internal interface IQuickPulseTopCpuCollector
    {
        bool InitializationFailed { get; }

        bool AccessDenied { get; }

        IEnumerable<Tuple<string, int>> GetTopProcessesByCpu(int topN);

        void Initialize();

        void Close();
    }
}