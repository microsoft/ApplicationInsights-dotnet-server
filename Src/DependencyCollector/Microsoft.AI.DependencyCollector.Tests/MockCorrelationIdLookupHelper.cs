namespace Microsoft.ApplicationInsights.DependencyCollector
{
    using Microsoft.ApplicationInsights.Common;
    using System.Collections.Generic;

    internal class MockCorrelationIdLookupHelper : ICorrelationIdLookupHelper
    {
        private readonly Dictionary<string, string> instrumentationKeyToCorrelationIdMap = new Dictionary<string, string>();

        public void SetCorrelationId(string instrumentationKey, string correlationId)
        {
            if (this.instrumentationKeyToCorrelationIdMap.ContainsKey(instrumentationKey))
            {
                this.instrumentationKeyToCorrelationIdMap[instrumentationKey] = correlationId;
            }
            else
            {
                this.instrumentationKeyToCorrelationIdMap.Add(instrumentationKey, correlationId);
            }
        }

        public bool TryGetXComponentCorrelationId(string instrumentationKey, out string correlationId)
        {
            bool result = this.instrumentationKeyToCorrelationIdMap.ContainsKey(instrumentationKey);
            correlationId = result ? this.instrumentationKeyToCorrelationIdMap[instrumentationKey] : string.Empty;
            return result;
        }
    }
}
