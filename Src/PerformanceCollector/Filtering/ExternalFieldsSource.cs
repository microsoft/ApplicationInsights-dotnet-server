namespace Microsoft.ApplicationInsights.Extensibility.Filtering
{
    /// <summary>
    /// Represents a source of external fields for type TTelemetry
    /// </summary>
    /// <typeparam name="TTelemetry"></typeparam>
    all external fields sources must derive from this
    internal interface IExternalFieldsSource<TTelemetry>
    {
    }
}