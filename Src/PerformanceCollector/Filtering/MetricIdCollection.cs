namespace Microsoft.ApplicationInsights.Extensibility.Filtering
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a collection of (SessionId, Id) pairs, all of which are distinct.
    /// </summary>
    internal class MetricIdCollection : HashSet<Tuple<string,string>>
    {
        public MetricIdCollection()
        {    
        }

        public MetricIdCollection(IEnumerable<Tuple<string, string>> ids)
            : base(ids)
        {
        }
    }
}