namespace Unit.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;

    using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.Implementation.QuickPulse;
    using Microsoft.ManagementServices.RealTimeDataProcessing.QuickPulseService;

    internal class QuickPulseServiceClientMock : IQuickPulseServiceClient
    {
        private readonly object countersLock = new object();

        public volatile bool CountersEnabled = true;

        public readonly object ResponseLock = new object();

        public int PingCount { get; private set; }

        public bool? ReturnValueFromPing { private get; set; }

        public bool? ReturnValueFromSubmitSample { private get; set; }

        public int? LastSampleBatchSize
        {
            get
            {
                lock (this.countersLock)
                {
                    return this.batches.Any() ? (int?)this.batches.Last().Item2.Count : null;
                }
            }
        }

        private List<Tuple<DateTimeOffset, List<QuickPulseDataSample>>> batches = new List<Tuple<DateTimeOffset, List<QuickPulseDataSample>>>();

        public DateTimeOffset? LastPingTimestamp { get; private set; }

        public string LastPingInstance { get; private set; }

        public bool AlwaysThrow { get; set; } = false;

        public List<QuickPulseDataSample> SnappedSamples
        {
            get
            {
                lock (this.countersLock)
                {
                    return this.batches.SelectMany(b => b.Item2).ToList();
                }
            }
        }

        public Uri ServiceUri { get; }

        public void Reset()
        {
            lock (this.countersLock)
            {
                this.PingCount = 0;
                this.LastPingTimestamp = null;
                this.LastPingInstance = string.Empty;
                this.batches.Clear();
            }
        }

        public bool? Ping(string instrumentationKey, DateTimeOffset timestamp)
        {
            lock (this.ResponseLock)
            {
                if (this.CountersEnabled)
                {
                    lock (this.countersLock)
                    {
                        this.PingCount++;
                        this.LastPingTimestamp = timestamp;
                    }
                }

                if (this.AlwaysThrow)
                {
                    throw new InvalidOperationException("Mock is set to always throw");
                }

                return this.ReturnValueFromPing;
            }
        }

        public bool? SubmitSamples(IEnumerable<QuickPulseDataSample> samples, string instrumentationKey, Clock timeProvider)
        {
            lock (this.ResponseLock)
            {
                if (this.CountersEnabled)
                {
                    lock (this.countersLock)
                    {
                        this.batches.Add(Tuple.Create(timeProvider.UtcNow, samples.ToList()));
                    }
                }

                if (this.AlwaysThrow)
                {
                    throw new InvalidOperationException("Mock is set to always throw");
                }

                return this.ReturnValueFromSubmitSample;
            }
        }
    }
}