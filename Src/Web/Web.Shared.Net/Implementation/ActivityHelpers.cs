namespace Microsoft.ApplicationInsights.Common
{
    using System;
    using System.Diagnostics;

    internal class ActivityHelpers
    {
        /// <summary>
        /// Name of the item under which Activity created by request tracking (if any) will be stored
        /// It's exactly the same as one Microsoft.AspNet.TelemetryCorrelation uses
        /// https://github.com/aspnet/Microsoft.AspNet.TelemetryCorrelation/blob/6ccf0729050be4fac6797fa85af0200883db1c83/src/Microsoft.AspNet.TelemetryCorrelation/ActivityHelper.cs#L33
        /// so that TelemetryCorrelation will restore and treat 'our' Activity as it's own.
        /// </summary>
        internal const string RequestActivityItemName = "__AspnetActivity__";

        internal static string RootOperationIdHeaderName { get; set; }

        internal static string ParentOperationIdHeaderName { get; set; }

        /// <summary> 
        /// Checks if given RequestId is hierarchical.
        /// </summary>
        /// <param name="requestId">Request id.</param>
        /// <returns>True if requestId is hierarchical false otherwise.</returns>
        internal static bool IsHierarchicalRequestId(string requestId)
        {
            return !string.IsNullOrEmpty(requestId) && requestId[0] == '|';
        }

        internal static string GetRootId(string legacyId)
        {
            Debug.Assert(!string.IsNullOrEmpty(legacyId), "diagnosticId must not be null or empty");

            if (legacyId[0] == '|')
            {
                var dot = legacyId.IndexOf('.');

                return legacyId.Substring(1, dot - 1);
            }

            return StringUtilities.EnforceMaxLength(legacyId, InjectionGuardConstants.RequestHeaderMaxLength);
        }

        internal static bool TryGetTraceId(string legacyId, out ReadOnlySpan<char> traceId)
        {
            Debug.Assert(!string.IsNullOrEmpty(legacyId), "diagnosticId must not be null or empty");

            traceId = default;
            if (legacyId[0] == '|' && legacyId.Length >= 33 && legacyId[33] == '.')
            {
                for (int i = 1; i < 33; i++)
                {
                    if (!((legacyId[i] >= '0' && legacyId[i] <= '9') || (legacyId[i] >= 'a' && legacyId[i] <= 'f')))
                    {
                        return false;
                    }
                }

                traceId = legacyId.AsSpan().Slice(1, 32);
                return true;
            }

            return false;
        }

        internal static string FormatTelemetryId(string traceId, string spanId)
        {
            return string.Concat('|', traceId, '.', spanId, '.');
        }
    }
}