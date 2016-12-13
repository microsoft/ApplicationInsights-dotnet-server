namespace Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.Implementation.QuickPulse.PerfLib
{
    using System;
    using System.Globalization;

    internal class PerfLib : IQuickPulsePerfLib
    {
        private static PerfLib library = null;

        private PerformanceMonitor performanceMonitor;

        private PerfLib()
        {
        }

        public static PerfLib GetPerfLib()
        {
            library = library ?? new PerfLib();

            return library;
        }

        public CategorySample GetCategorySample(int categoryIndex, int counterIndex)
        {
            byte[] dataRef = this.GetPerformanceData(categoryIndex.ToString(CultureInfo.InvariantCulture));
            if (dataRef == null)
            {
                throw new InvalidOperationException("Could not read data for category index " + categoryIndex);
            }

            return new CategorySample(dataRef, categoryIndex, counterIndex, this);
        }

        public void Initialize()
        {
            this.performanceMonitor = new PerformanceMonitor();
        }

        public void Close()
        {
            this.performanceMonitor?.Close();

            library = null;
        }

        public byte[] GetPerformanceData(string categoryIndex)
        {
            return this.performanceMonitor.GetData(categoryIndex);
        }
    }
}
