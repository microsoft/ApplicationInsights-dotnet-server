using System;

namespace Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.Implementation.QuickPulse.Helpers
{
    internal interface IQuickPulseModuleSchedulerHandle : IDisposable
    {
        void Stop(bool wait);
    }
}
