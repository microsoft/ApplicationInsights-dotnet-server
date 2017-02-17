namespace Microsoft.ApplicationInsights.DependencyCollector.Implementation
{
    using System;   

    /// <summary>
    /// Dependency TargetName helper.
    /// </summary>
    internal static class DependencyTargetNameHelper
    {
        private const int HttpPort = 80;
        private const int HttpsPort = 443;

        /// <summary>
        /// Returns dependency target name from the given Uri.
        /// Port name is included in target for non-standard ports.
        /// </summary>
        /// <param name="uri">Dependency url from which target is to be extracted.</param>        
        /// <returns>Dependency target name.</returns>
        internal static string GetDependencyTargetName(Uri uri)
        {
            if (uri.Port != HttpPort && uri.Port != HttpsPort)
            {
                return uri.Host + ":" + uri.Port;
            }
            else
            {
                return uri.Host;
            }
        }      
    }
}
