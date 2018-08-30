namespace Microsoft.ApplicationInsights.Extensibility.Filtering
{
    using System.Linq;
    using System.Runtime.Serialization;
    using Microsoft.ApplicationInsights.Common;

    /// <summary>
    /// An AND-connected group of FilterInfo objects.
    /// </summary>
    [DataContract]
    internal class FilterConjunctionGroupInfo
    {
        [DataMember]
        public FilterInfo[] Filters { get; set; }

        public override string ToString()
        {
            return string.Join(", ", (this.Filters ?? ArrayExtensions.Empty<FilterInfo>()).Select(filter => filter.ToString()));
        }
    }
}