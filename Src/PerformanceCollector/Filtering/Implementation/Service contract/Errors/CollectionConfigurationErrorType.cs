namespace Microsoft.ApplicationInsights.Extensibility.Filtering
{
    internal enum CollectionConfigurationErrorType
    {
        PerformanceCounterParsing,

        PerformanceCounterUnexpected,

        DocumentStreamDuplicateIds,

        DocumentStreamFailureToCreate,

        DocumentStreamFailureToCreateFilterUnexpected,

        //DocumentStreamFilterFailureToRun,

        MetricDuplicateIds,

        MetricTelemetryTypeUnsupported,

        MetricFailureToCreate,

        MetricFailureToCreateFilterUnexpected,

        FilterFailureToCreateUnexpected,

        //FilterFailureToRun
    }
}
