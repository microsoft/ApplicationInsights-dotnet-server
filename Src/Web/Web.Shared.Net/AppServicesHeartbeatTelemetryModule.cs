namespace Microsoft.ApplicationInsights.Web
{
    using System;
    using System.Collections.Generic;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;
    using Microsoft.ApplicationInsights.Extensibility.Implementation.Tracing;
    using Microsoft.ApplicationInsights.Web.Implementation;

    /// <summary>
    /// Provides default values for the heartbeat feature of Application Insights that
    /// are specific to Azure App Services (Web Apps, Functions, etc...).
    /// </summary>
    public class AppServicesHeartbeatTelemetryModule : ITelemetryModule
    {
        internal const int SiteNameMaxLength = 256;
        internal const int StampNameMaxLength = 256;
        internal const int HostNameMaxLength = 256;

        /// <summary>
        /// Environment variables and the Application Insights heartbeat field names that accompany them.
        /// </summary>
        internal readonly KeyValuePair<string, string>[] WebHeartbeatPropertyNameEnvVarMap = new KeyValuePair<string, string>[]
        {
            new KeyValuePair<string, string>("appSrv_SiteName", "WEBSITE_SITE_NAME"),
            new KeyValuePair<string, string>("appSrv_wsStamp", "WEBSITE_HOME_STAMPNAME"),
            new KeyValuePair<string, string>("appSrv_wsHost", "WEBSITE_HOSTNAME")
        };

        private object lockObject = new object();
        private bool isInitialized = false;

        /// <summary>
        /// Initialize the default heartbeat provider for Azure App Services. This module
        /// looks for specific environment variables and sets them into the heartbeat 
        /// properties for Application Insights, if they exist.
        /// </summary>
        /// <param name="configuration">Unused parameter.</param>
        public void Initialize(TelemetryConfiguration configuration)
        {
            // Core SDK creates 1 instance of a module but calls Initialize multiple times
            if (!this.isInitialized)
            {
                lock (this.lockObject)
                {
                    if (!this.isInitialized)
                    {
                        this.AddAppServiceEnvironmentVariablesToHeartbeat();

                        this.isInitialized = true;
                    }
                }
            }
        }

        internal void AddAppServiceEnvironmentVariablesToHeartbeat()
        {
            var telemetryModules = TelemetryModules.Instance;
            Dictionary<string, string> hbeatProps = new Dictionary<string, string>();

            IHeartbeatPropertyManager hbeatManager = null;
            try
            {
                foreach (var module in telemetryModules.Modules)
                {
                    if (module is IHeartbeatPropertyManager hman)
                    {
                        hbeatManager = hman;
                    }
                }
            }
            catch (Exception hearbeatManagerAccessException)
            {
                WebEventSource.Log.HeartbeatManagerAccessFailure(hearbeatManagerAccessException.Message);
            }


            foreach (var kvp in WebHeartbeatPropertyNameEnvVarMap)
            {
                try
                {
                    // get the variable, then expand it (otherwise we get the name we queried for in the value)
                    string varValue = Environment.GetEnvironmentVariable(kvp.Value);
                    if (!string.IsNullOrEmpty(varValue))
                    {
                        varValue = Environment.ExpandEnvironmentVariables(varValue);
                        string varName = kvp.Key.ToString();
                        hbeatManager.AddHeartbeatProperty(varName, varValue, true);
                    }
                }
                catch (Exception heartbeatValueException)
                {
                    WebEventSource.Log.HeartbeatPropertyAquisitionFailed(kvp.Value, heartbeatValueException.Message);
                }
            }
        }
    }
}