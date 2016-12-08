namespace Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.Implementation.QuickPulse
{
    using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.Implementation.QuickPulse.PerfLib;

    /// <summary>
    /// Interface for the <c>PerfLib</c>.
    /// </summary>
    internal interface IQuickPulsePerfLib
    {
        CategorySample GetCategorySample(int categoryIndex, int counterIndex);

        void Close();
    }
}