namespace Unit.Tests
{
    using System;
    using System.Collections.Generic;

    using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.Implementation.QuickPulse;

    internal class QuickPulseProcessProviderMock : IQuickPulseProcessProvider
    {
        public List<QuickPulseProcess> Processes { get; set; }

        public bool AlwaysThrow { get; set; } = false;

        public TimeSpan? OverallTimeValue { get; set; } = null;

        public void Close()
        {
        }

        public IEnumerable<QuickPulseProcess> GetProcesses(out TimeSpan? totalTime)
        {
            totalTime = this.OverallTimeValue;

            if (this.AlwaysThrow)
            {
                throw new Exception("Mock is configured to always throw");
            }

            return this.Processes;
        }
    }
}