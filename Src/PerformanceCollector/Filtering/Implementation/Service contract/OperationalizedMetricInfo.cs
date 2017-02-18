namespace Microsoft.ApplicationInsights.Extensibility.Filtering
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.Serialization;

    [DataContract]
    internal class OperationalizedMetricInfo
    {
        [DataMember]
        public string Id { get; set; }

        [DataMember]
        public TelemetryType TelemetryType { get; set; }

        [DataMember]
        public FilterInfo[] Filters { get; set; }

        [DataMember]
        public string Projection { get; set; }

        [DataMember]
        public AggregationType Aggregation { get; set; }
    }
}