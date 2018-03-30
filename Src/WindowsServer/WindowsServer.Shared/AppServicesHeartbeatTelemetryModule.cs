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
        /// <summary>
        /// Environment variables and the Application Insights heartbeat field names that accompany them.
        /// </summary>
        internal static readonly KeyValuePair<string, string>[] WebHeartbeatPropertyNameEnvVarMap = new KeyValuePair<string, string>[]
        {
            new KeyValuePair<string, string>("appSrv_SiteName", "WEBSITE_SITE_NAME"),
            new KeyValuePair<string, string>("appSrv_wsStamp", "WEBSITE_HOME_STAMPNAME"),
            new KeyValuePair<string, string>("appSrv_wsHost", "WEBSITE_HOSTNAME")
        };

        // for testing only: override the heartbeat manager
        internal IHeartbeatPropertyManager HeartbeatManager;

        // to provide a 'singleton' accessor for updating the env vars should they change during runtime
        private static AppServicesHeartbeatTelemetryModule instance;
        private bool isInitialized = false;
        
        /// <summary>
        /// Initializes a new instance of the<see cref="AppServicesHeartbeatTelemetryModule" /> class.
        /// </summary>
        public AppServicesHeartbeatTelemetryModule() : this(null)
        {            
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AppServicesHeartbeatTelemetryModule" /> class. This is
        /// internal, and allows for overriding the Heartbeat Property Manager to test this module with.
        /// </summary>
        /// <param name="hbeatPropManager">The heartbeat property manager to use when setting/updating env var values.</param>
        internal AppServicesHeartbeatTelemetryModule(IHeartbeatPropertyManager hbeatPropManager)
        {
            this.HeartbeatManager = hbeatPropManager;
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
            this.isInitialized = this.UpdateHeartbeatWithAppServiceEnvVarValues();
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
        /// <returns>A value indicating whether or not an update to the environment variables occurred.</returns>
        public bool UpdateHeartbeatWithAppServiceEnvVarValues()
        {
            bool hasBeenUpdated = false;

            try
            {
                var hbeatManager = this.GetHeartbeatPropertyManager();
                if (hbeatManager != null)
                {
                    hasBeenUpdated = this.AddAppServiceEnvironmentVariablesToHeartbeat(hbeatManager, isUpdateOperation: this.isInitialized);
                }
            }
            catch (Exception appSrvEnvVarHbeatFailure)
            {
                WindowsServerEventSource.Log.AppServiceHeartbeatPropertySettingFails(appSrvEnvVarHbeatFailure.ToInvariantString());
            }

            return hasBeenUpdated;
        }

        internal bool AddAppServiceEnvironmentVariablesToHeartbeat(IHeartbeatPropertyManager hbeatManager, bool isUpdateOperation = false)
        {
            bool hasBeenUpdated = false;

            if (hbeatManager == null)
            {
                WindowsServerEventSource.Log.AppServiceHeartbeatSetCalledWithNullManager();
            }
            else
            {
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

                        hasBeenUpdated = true;
                    }
                    catch (Exception heartbeatValueException)
                    {
                        WindowsServerEventSource.Log.AppServiceHeartbeatPropertyAquisitionFailed(kvp.Value, heartbeatValueException.ToInvariantString());
                    }
                }
            }

            return hasBeenUpdated;
        }

        private IHeartbeatPropertyManager GetHeartbeatPropertyManager()
        {
            if (this.HeartbeatManager != null)
            {
                // early exit - this is the internal test scenario
                return this.HeartbeatManager;
            }

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
    }
}