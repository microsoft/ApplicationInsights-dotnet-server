namespace Microsoft.ApplicationInsights.Extensibility.Filtering
{
    using System.Runtime.Serialization;

    [DataContract]
    internal class FilterInfo
    {
        [DataMember]
        public string SessionId { get; set; }

        [DataMember]
        public TelemetryType TelemetryType { get; set; }

        [DataMember]
        public string FieldName { get; set; }

        [DataMember]
        public Predicate Predicate { get; set; }

        [DataMember]
        public string Comparand { get; set; }

        [DataMember]
        public string Projection { get; set; }
    }
}
