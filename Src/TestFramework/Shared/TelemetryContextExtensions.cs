namespace Microsoft.ApplicationInsights.TestFramework
{
    using System.Collections.Generic;
    using System.Reflection;
    using Microsoft.ApplicationInsights.DataContracts;

    internal static class TelemetryContextExtensions
    {
        internal static Dictionary<string, string> GetCorrelationContext(this TelemetryContext context)
        {
            BindingFlags bindFlags = BindingFlags.Instance | BindingFlags.NonPublic;
            var property = typeof(TelemetryContext).GetProperty("CorrelationContext", bindFlags);
            return (Dictionary<string, string>)property.GetValue(context, null);
        }
    }
}
