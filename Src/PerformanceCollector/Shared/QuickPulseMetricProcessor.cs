namespace Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse
{
    using System;
    using System.Threading;

    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.Filtering;
    using Microsoft.ApplicationInsights.Extensibility.Implementation.Tracing;
    using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.Implementation.QuickPulse;

    /// <summary>
    /// Metric processor for collecting QuickPulse data.
    /// </summary>
    internal sealed class QuickPulseMetricProcessor : IMetricProcessor
    {
        private bool isCollecting = false;

        private IQuickPulseDataAccumulatorManager dataAccumulatorManager;

        public void StartCollection(IQuickPulseDataAccumulatorManager accumulatorManager)
        {
            if (accumulatorManager == null)
            {
                throw new ArgumentNullException(nameof(accumulatorManager));
            }

            if (this.isCollecting)
            {
                throw new InvalidOperationException("Can't start collection while it is already running.");
            }

            this.dataAccumulatorManager = accumulatorManager;
            
            this.isCollecting = true;
        }

        public void StopCollection()
        {
            this.isCollecting = false;
            this.dataAccumulatorManager = null;
        }
        
        public void Track(Metric metric, double value)
        {
            try
            {
                if (!this.isCollecting || this.dataAccumulatorManager == null || metric == null)
                {
                    return;
                }

                // get a local reference, the accumulator might get swapped out a any time
                // in case we continue to process this configuration once the accumulator is out, increase the reference count so that this accumulator is not sent out before we're done
                CollectionConfigurationAccumulator configurationAccumulatorLocal =
                    this.dataAccumulatorManager.CurrentDataAccumulator.CollectionConfigurationAccumulator;

                //!!! better solution?
                // if the accumulator is swapped out, a sample is created and sent out - all while between these two lines, this telemetry item gets lost
                // however, that is not likely to happen

                Interlocked.Increment(ref configurationAccumulatorLocal.ReferenceCount);
                try
                {
                    CollectionConfigurationError[] filteringErrors;
                    string projectionError = null;

                    QuickPulseTelemetryProcessor.ProcessMetrics(
                        configurationAccumulatorLocal,
                        configurationAccumulatorLocal.CollectionConfiguration.MetricMetrics,
                        new MetricValue(metric, value),
                        out filteringErrors,
                        ref projectionError);
                   
                    //!!! report errors from string[] errors; and string projectionError;
                }
                finally
                {
                    Interlocked.Decrement(ref configurationAccumulatorLocal.ReferenceCount);
                }
            }
            catch (Exception e)
            {
                // whatever happened up there - we don't want to interrupt the flow of telemetry
                QuickPulseEventSource.Log.UnknownErrorEvent(e.ToInvariantString());
            }
        }
    }
}