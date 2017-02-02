using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.ApplicationInsights.Extensibility.Filtering
{
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.DataContracts;

    public class FilterInfo
    {
        public string FieldName { get; set; }

        public Predicate Predicate { get; set; }

        public string Comparand { get; set; }

        public string Projection { get; set; }
    }
}
