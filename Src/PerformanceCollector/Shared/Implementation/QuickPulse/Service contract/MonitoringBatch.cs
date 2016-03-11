namespace Microsoft.ManagementServices.RealTimeDataProcessing.QuickPulseService
{
    using System;
    using System.Runtime.Serialization;

    [DataContract]
    internal struct MonitoringBatch
    {
        [DataMember]
        public DateTime Timestamp { get; set; }

        [DataMember]
        public MonitoringDataPoint[] DataPoints { get; set; }
    }
}