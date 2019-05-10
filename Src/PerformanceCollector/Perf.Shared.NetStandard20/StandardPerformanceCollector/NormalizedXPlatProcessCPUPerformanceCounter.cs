namespace Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.Implementation.StandardPerformanceCollector
{
    using System;
    using System.Diagnostics;
    using System.Globalization;

    /// <summary>
    /// Represents normalized value of CPU Utilization by Process counter value (divided by the processors count).
    /// </summary>
    internal class NormalizedXPlatProcessCPUPerformanceCounter : ICounterValue
    {
        private double lastCollectedValue = 0;
        private DateTimeOffset lastCollectedTime = DateTimeOffset.MinValue;
        private readonly int CoreCount = Environment.ProcessorCount;

        /// <summary>
        ///  Initializes a new instance of the <see cref="NormalizedProcessCPUPerformanceCounter" /> class.
        /// </summary>
        internal NormalizedXPlatProcessCPUPerformanceCounter()
        {
            this.lastCollectedValue = Process.GetCurrentProcess().TotalProcessorTime.Ticks;
            this.lastCollectedTime = DateTimeOffset.UtcNow;
        }

        /// <summary>
        /// Returns the current value of the counter as a <c ref="MetricTelemetry"/>.
        /// </summary>
        /// <returns>Value of the counter.</returns>
        public double Collect()
        {
            try
            {
                double previouslyCollectedValue = this.lastCollectedValue;
                this.lastCollectedValue = Process.GetCurrentProcess().TotalProcessorTime.Ticks;

                var previouslyCollectedTime = this.lastCollectedTime;
                this.lastCollectedTime = DateTimeOffset.UtcNow;

                double value = 0;
                if (previouslyCollectedTime != DateTimeOffset.MinValue)
                {
                    var baseValue = this.lastCollectedTime.Ticks - previouslyCollectedTime.Ticks;
                    baseValue = baseValue != 0 ? baseValue : 1;

                    var diff = this.lastCollectedValue - previouslyCollectedValue;

                    if (diff < 0)
                    {
                        
                    }
                    else
                    {
                        value = (double)(diff * 100.0 / baseValue);
                    }
                }

                return value;
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        "Failed to perform a read for performance counter NormalizedXPlatProcessCPUPerformanceCounter. Exception: {0}", e));
            }
        }
    }
}
