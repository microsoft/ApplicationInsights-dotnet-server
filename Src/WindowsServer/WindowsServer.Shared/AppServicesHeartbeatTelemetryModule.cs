namespace Microsoft.ApplicationInsights.WindowsServer
{
    using System;
    using System.Collections.Generic;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;
    using Microsoft.ApplicationInsights.Extensibility.Implementation.Tracing;
    using Microsoft.ApplicationInsights.WindowsServer.Implementation;

    /// <summary>
    /// Provides default values for the heartbeat feature of Application Insights that
    /// are specific to Azure App Services (Web Apps, Functions, etc...).
    /// </summary>
    public sealed class AppServicesHeartbeatTelemetryModule : ITelemetryModule, IDisposable
    {
        internal const int SiteNameMaxLength = 256;
        internal const int StampNameMaxLength = 256;
        internal const int HostNameMaxLength = 256;

        /// <summary>
        /// Environment variables and the Application Insights heartbeat field names that accompany them.
        /// </summary>
        internal static readonly KeyValuePair<string, string>[] WebHeartbeatPropertyNameEnvVarMap = new KeyValuePair<string, string>[]
        {
            new KeyValuePair<string, string>("appSrv_SiteName", "WEBSITE_SITE_NAME"),
            new KeyValuePair<string, string>("appSrv_wsStamp", "WEBSITE_HOME_STAMPNAME"),
            new KeyValuePair<string, string>("appSrv_wsHost", "WEBSITE_HOSTNAME")
        };

        private static AppServicesHeartbeatTelemetryModule instance;
        private object lockObject = new object();
        private bool isInitialized = false;

        /// <summary>
        /// Initializes a new instance of the<see cref="AppServicesHeartbeatTelemetryModule" /> class.
        /// </summary>
        public AppServicesHeartbeatTelemetryModule()
        {
            AppServicesHeartbeatTelemetryModule.Instance = this;
        }

        /// <summary>
        /// Gets a value to provide internal access to the only instance of this class that *should* be available.
        /// </summary>
        public static AppServicesHeartbeatTelemetryModule Instance
        {
            get => AppServicesHeartbeatTelemetryModule.instance;
            private set => AppServicesHeartbeatTelemetryModule.instance = value;
        }

        /// <summary>
        /// Initialize the default heartbeat provider for Azure App Services. This module
        /// looks for specific environment variables and sets them into the heartbeat 
        /// properties for Application Insights, if they exist.
        /// </summary>
        /// <param name="configuration">Unused parameter.</param>
        public void Initialize(TelemetryConfiguration configuration)
        {
            this.UpdateHeartbeatWithAppServiceEnvVarValues();
        }

        /// <summary>
        /// Ensure we've cleaned up our static Instance.
        /// </summary>
        public void Dispose()
        {
            // ensure our Instance variable is not kept around.
            AppServicesHeartbeatTelemetryModule.instance = null;
        }

        /// <summary>
        /// Signal the AppServicesHeartbeatTelemetryModule to update the values of the 
        /// Environment variables we use in our heartbeat payload.
        /// </summary>
        public void UpdateHeartbeatWithAppServiceEnvVarValues()
        {
            try
            {
                var hbeatManager = this.GetHeartbeatPropertyManager();
                if (hbeatManager != null)
                {
                    this.AddAppServiceEnvironmentVariablesToHeartbeat(hbeatManager, isUpdateOperation: this.isInitialized);
                }
            }
            catch (Exception appSrvEnvVarHbeatFailure)
            {
                WindowsServerEventSource.Log.AppServiceHeartbeatPropertySettingFails(appSrvEnvVarHbeatFailure.ToInvariantString());
            }
        }

        private IHeartbeatPropertyManager GetHeartbeatPropertyManager()
        {
            IHeartbeatPropertyManager hbeatManager = null;
            var telemetryModules = TelemetryModules.Instance;

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
                WindowsServerEventSource.Log.AppServiceHeartbeatManagerAccessFailure(hearbeatManagerAccessException.ToInvariantString());
            }

            if (hbeatManager == null)
            {
                WindowsServerEventSource.Log.AppServiceHeartbeatManagerNotAvailable();
            }

            return hbeatManager;
        }

        private void AddAppServiceEnvironmentVariablesToHeartbeat(IHeartbeatPropertyManager hbeatManager, bool isUpdateOperation = false)
        {
            if (hbeatManager == null)
            {
                WindowsServerEventSource.Log.AppServiceHeartbeatSetCalledWithNullManager();
                return;
            }

            foreach (var kvp in WebHeartbeatPropertyNameEnvVarMap)
            {
                try
                {
                    // get the variable, then expand it (otherwise we get the name we queried for in the value)
                    string hbeatValue = Environment.GetEnvironmentVariable(kvp.Value);
                    if (!string.IsNullOrEmpty(hbeatValue))
                    {
                        hbeatValue = Environment.ExpandEnvironmentVariables(hbeatValue);
                        string hbeatKey = kvp.Key.ToString();
                        if (isUpdateOperation)
                        {
                            hbeatManager.SetHeartbeatProperty(hbeatKey, hbeatValue);
                        }
                        else
                        {
                            hbeatManager.AddHeartbeatProperty(hbeatKey, hbeatValue, true);
                        }
                    }
                }
                catch (Exception heartbeatValueException)
                {
                    WindowsServerEventSource.Log.AppServiceHeartbeatPropertyAquisitionFailed(kvp.Value, heartbeatValueException.ToInvariantString());
                }
            }
        }
    }
}