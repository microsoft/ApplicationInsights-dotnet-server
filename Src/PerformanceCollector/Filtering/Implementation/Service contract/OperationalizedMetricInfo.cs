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
        public string SessionId { get; set; }

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

        #region Comparability overloads

        public static bool operator ==(OperationalizedMetricInfo left, OperationalizedMetricInfo right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
            {
                return false;
            }

            return FilterInfo.Equals(left, right);
        }

        public static bool operator !=(OperationalizedMetricInfo left, OperationalizedMetricInfo right)
        {
            return !(left == right);
        }

        public override string ToString()
        {
            // this is effectively a hash code
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}({1}): {2} Filters: [{3}]",
                this.TelemetryType,
                this.Aggregation,
                this.Projection,
                this.Filters.Length > 0 ? string.Join(", ", this.Filters.Select(filter => filter.ToString())) : string.Empty);
        }

        public override int GetHashCode()
        {
            throw new InvalidOperationException("Calculating hash is not supported.");
        }

        public override bool Equals(object obj)
        {
            OperationalizedMetricInfo arg = obj as OperationalizedMetricInfo;

            return !ReferenceEquals(arg, null) && OperationalizedMetricInfo.Equals(this, arg);
        }

        public bool Equals(OperationalizedMetricInfo arg)
        {
            return !ReferenceEquals(arg, null) && OperationalizedMetricInfo.Equals(this, arg);
        }

        private static bool Equals(OperationalizedMetricInfo left, OperationalizedMetricInfo right)
        {
            // Id and SessionId do not affect equality
            return left.TelemetryType == right.TelemetryType && string.Equals(left.Projection, right.Projection, StringComparison.Ordinal)
                   && left.Aggregation == right.Aggregation && FiltersEqual(left.Filters, right.Filters);
        }

        private static bool FiltersEqual(IEnumerable<FilterInfo> left, IEnumerable<FilterInfo> right)
        {
            return left.OrderBy(filter => filter.ToString()).SequenceEqual(right.OrderBy(filter => filter.ToString()));
        }

        #endregion
    }
}