namespace Microsoft.ApplicationInsights.Extensibility.Filtering
{
    using System;
    using System.Globalization;
    using System.Runtime.Serialization;

    [DataContract]
    internal class FilterInfo
    {
        [DataMember]
        public string FieldName { get; set; }

        [DataMember]
        public Predicate Predicate { get; set; }

        [DataMember]
        public string Comparand { get; set; }

        #region Comparability overloads
        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} {1} {2}", this.FieldName, this.Predicate, this.Comparand);
        }

        public override int GetHashCode()
        {
            throw new InvalidOperationException("Hash calculation is not supported.");
        }

        public override bool Equals(object obj)
        {
            FilterInfo arg = obj as FilterInfo;

            return arg != null && FilterInfo.Equals(this, arg);
        }

        public static bool operator ==(FilterInfo left, FilterInfo right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            return FilterInfo.Equals(left, right);
        }

        public static bool operator !=(FilterInfo left, FilterInfo right)
        {
            return !(left == right);
        }

        private static bool Equals(FilterInfo left, FilterInfo right)
        {
            return string.Equals(left.FieldName, right.FieldName, StringComparison.Ordinal) && left.Predicate == right.Predicate
                  && string.Equals(left.Comparand, right.Comparand, StringComparison.Ordinal);
        }
        #endregion
    }
}