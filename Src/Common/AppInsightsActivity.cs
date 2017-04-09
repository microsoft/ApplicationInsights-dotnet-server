namespace Microsoft.ApplicationInsights.Common
{
    using System;
    using System.Diagnostics;
    using System.Threading;

    //this is a temporary solution that mimics System.DiagnosticSource,Activity and Correlation HTTP protocol:
    //https://github.com/lmolkova/correlation/blob/master/http_protocol_proposal_v1.md
    //It does not implement 
    // - Request-Id length limitation and overflow
    internal class AppInsightsActivity
    {
        internal static string GetRootId(string requestId)
        {
            Debug.Assert(!string.IsNullOrEmpty(requestId));
            if (requestId[0] == '|')
            {
                var rootEnd = requestId.IndexOf('.');
                var rootId = requestId.Substring(1, rootEnd - 1);
                return rootId;
            }
            return requestId;
        }


        internal static string GenerateNewId()
        {
            if (_machinePrefix == null)
                Interlocked.CompareExchange(ref _machinePrefix, Environment.MachineName + "-" + ((int)Stopwatch.GetTimestamp()).ToString("x"), null);
            return '|' + _machinePrefix + '-' + Interlocked.Increment(ref _currentOperationNum).ToString("x") + '.';
        }

        internal static string GenerateRequestId(string parentRequestId, string telemetryId)
        {
            if (parentRequestId != null)
            {
                var childRequestId = parentRequestId[0] != '|' ? '|' + parentRequestId : parentRequestId;
                if (childRequestId[childRequestId.Length - 1] != '.')
                    childRequestId += '.';

                return GenerateChildTelemetryId(childRequestId, telemetryId, '_');
            }
            return GenerateNewId();
        }

        internal static string GenerateDependencyId(string parentRequestId, string telemetryId)
        {
            if (parentRequestId != null)
                return GenerateChildTelemetryId(parentRequestId, telemetryId, '.');

            return GenerateNewId();
        }

        private static string _machinePrefix;
        private static long _currentOperationNum = BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(), 12);

        private static string GenerateChildTelemetryId(string parentId, string telemetryId, char delimiter)
        {
            Debug.Assert(!string.IsNullOrEmpty(parentId));
            Debug.Assert(!string.IsNullOrEmpty(telemetryId));
            return parentId + telemetryId + delimiter;
        }
    }
}
