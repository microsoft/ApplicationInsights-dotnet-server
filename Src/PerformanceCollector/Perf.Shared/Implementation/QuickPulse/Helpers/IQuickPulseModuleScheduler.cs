using System;
using System.Threading;

namespace Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.Implementation.QuickPulse.Helpers
{
    internal interface IQuickPulseModuleScheduler
    {
        IQuickPulseModuleSchedulerHandle Execute(Action<CancellationToken> action);
    }
}
