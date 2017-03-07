namespace Microsoft.ApplicationInsights.Extensibility.Filtering
{
    using System.Runtime.Serialization;

    /// <summary>
    /// Represents the entire collection configuration for this SDK.
    /// </summary>
    [DataContract]
    internal class CollectionConfigurationInfo
    {
        [DataMember]
        public string ETag { get; set; }

        [DataMember]
        public OperationalizedMetricInfo[] Metrics { get; set; }

        [DataMember]
        public DocumentStreamInfo[] DocumentStreams { get; set; }
    }
}
