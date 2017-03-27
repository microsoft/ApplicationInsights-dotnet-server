namespace Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.Implementation.QuickPulse
{
    using System;
    using System.Threading.Tasks;

    internal interface IQuickPulseWebSocket : IDisposable
    {
        bool IsConnected { get; }

        Task StartAsync(Action<string> onMessage);

        Task StopAsync();
    }
}