using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.ApplicationInsights.Extensibility.Filtering
{
    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.DataContracts;

    public enum Predicate
    {
        Equals,

        LessThan,

        GreaterThan,

        LessOrEqual,

        GreaterOrEqual,

        Contains
    }
}
