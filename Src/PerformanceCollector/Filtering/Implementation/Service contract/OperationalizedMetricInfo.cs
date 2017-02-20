namespace Microsoft.ApplicationInsights.Extensibility.Filtering
{
    using System;
    using System.Globalization;
    using System.Runtime.Serialization;

    [DataContract]
    internal class OperationalizedMetricInfo
    {
        [DataMember]
        public string Id { get; set; }

        [DataMember(Name = "TelemetryType")]
        public string TelemetryTypeForSerialization
        {
            get
            {
                return this.TelemetryType.ToString();
            }

            set
            {
                TelemetryType telemetryType;
                if (!Enum.TryParse(value, out telemetryType))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        string.Format(CultureInfo.InvariantCulture, "Unsupported TelemetryType value: {0}", value));
                }

                this.TelemetryType = telemetryType;
            }
        }

        public TelemetryType TelemetryType { get; set; }

        [DataMember]
        public FilterInfo[] Filters { get; set; }

        [DataMember]
        public string Projection { get; set; }

        [DataMember(Name = "Aggregation")]
        public string AggregationForSerialization
        {
            get
            {
                return this.Aggregation.ToString();
            }

            set
            {
                AggregationType aggregation;
                if (!Enum.TryParse(value, out aggregation))
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        string.Format(CultureInfo.InvariantCulture, "Unsupported Aggregation value: {0}", value));
                }

                this.Aggregation = aggregation;
            }
        }

        public AggregationType Aggregation { get; set; }
    }
}