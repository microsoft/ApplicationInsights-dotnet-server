namespace Microsoft.ApplicationInsights.Common
{
    /// <summary>
    /// An interface for getting a correlation id from a provided instrumentation key.
    /// </summary>
    internal interface ICorrelationIdLookupHelper
    {
        /// <summary>
        /// Gets the empty correlation id based on the correlation id format. This is sent as a header to indicate that we are dealing with a tracked component, but we could not fetch the appId just yet.
        /// </summary>
        string EmptyCorrelationId { get; }

        /// <summary>
        /// Retrieves the correlation id corresponding to a given instrumentation key.
        /// </summary>
        /// <param name="instrumentationKey">Instrumentation key string.</param>
        /// <param name="correlationId">AppId corresponding to the provided instrumentation key.</param>
        /// <returns>true if correlationId was successfully retrieved; false otherwise.</returns>
        bool TryGetXComponentCorrelationId(string instrumentationKey, out string correlationId);
    }
}
