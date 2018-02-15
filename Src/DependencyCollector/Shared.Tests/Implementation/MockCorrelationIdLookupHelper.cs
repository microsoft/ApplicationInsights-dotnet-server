namespace Microsoft.ApplicationInsights.Tests
{
    using System;
    using System.Collections.Generic;
    using Microsoft.ApplicationInsights.Common;

    internal class MockCorrelationIdLookupHelper : ICorrelationIdLookupHelper
    {
        private readonly Dictionary<string, string> instrumentationKeyToCorrelationIdMap;
        
        public MockCorrelationIdLookupHelper(Dictionary<string, string> instrumentationKeyToCorrelationIdMap)
        {
            this.instrumentationKeyToCorrelationIdMap = instrumentationKeyToCorrelationIdMap;
        }

        public string EmptyCorrelationId
        {
            get
            {
                return "cid-v1:";
            }
        }

        public bool TryGetXComponentCorrelationId(string instrumentationKey, out string correlationId)
        {
            return this.instrumentationKeyToCorrelationIdMap.TryGetValue(instrumentationKey, out correlationId);
        }
    }
}
