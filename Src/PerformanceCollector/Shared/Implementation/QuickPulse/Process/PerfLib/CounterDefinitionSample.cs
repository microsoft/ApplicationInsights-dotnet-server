namespace Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.Implementation.QuickPulse.PerfLib
{
    using System;
    using System.Runtime.InteropServices;

    internal class CounterDefinitionSample
    {
        internal readonly int NameIndex;

        internal readonly int CounterType;

        internal long[] InstanceValues;

        private readonly int size;

        private readonly int offset;
        
        public CounterDefinitionSample(NativeMethods.PERF_COUNTER_DEFINITION perfCounter, int instanceNumber)
        {
            this.NameIndex = perfCounter.CounterNameTitleIndex;
            this.CounterType = perfCounter.CounterType;
            this.offset = perfCounter.CounterOffset;
            this.size = perfCounter.CounterSize;
            this.InstanceValues = instanceNumber == -1 ? new long[1] : new long[instanceNumber];
        }

        public void SetInstanceValue(int index, IntPtr dataRef)
        {
            long rawValue = this.ReadValue(dataRef);
            this.InstanceValues[index] = rawValue;
        }

        public long GetInstanceValue(int instanceIndex)
        {
            return this.InstanceValues[instanceIndex];
        }

        private long ReadValue(IntPtr pointer)
        {
            if (this.size == 4)
            {
                return (long)(uint)Marshal.ReadInt32((IntPtr)((long)pointer + this.offset));
            }
            else if (this.size == 8)
            {
                return (long)Marshal.ReadInt64((IntPtr)((long)pointer + this.offset));
            }

            return -1;
        }
    }
}