namespace Microsoft.ApplicationInsights.WindowsServer
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Runtime.Serialization.Json;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.WindowsServer.Implementation;
    using Microsoft.ApplicationInsights.WindowsServer.Implementation.DataContracts;
    using Microsoft.ApplicationInsights.WindowsServer.Mock;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Assert = Xunit.Assert;

    [TestClass]
    public class AppServicesHeartbeatTelemetryModuleTests
    {
        [TestMethod]
        public void InitializeIsWorking()
        {
            var hbeatProviderMock = new HeartbeatProviderMock();
            var appSrvHbeatModule = this.GetAppServiceHeartbeatModuleWithUniqueTestEnvVars(hbeatProviderMock);
            var envVars = this.GetEnvVarsAssociatedToModule(appSrvHbeatModule);

            appSrvHbeatModule.Initialize(null);

            foreach (var kvp in appSrvHbeatModule.WebHeartbeatPropertyNameEnvVarMap)
            {
                Assert.True(hbeatProviderMock.HbeatProps.ContainsKey(kvp.Key));
                Assert.Equal(hbeatProviderMock.HbeatProps[kvp.Key], envVars[kvp.Value]);
            }

            this.RemoveTestEnvVarsAssociatedToModule(appSrvHbeatModule);
        }

        [TestMethod]
        public void UpdateEnvVarsWorksWhenEnvironmentValuesChange()
        {            
            var hbeatProviderMock = new HeartbeatProviderMock();
            var appSrvHbeatModule = this.GetAppServiceHeartbeatModuleWithUniqueTestEnvVars(hbeatProviderMock);
            var envVars = this.GetEnvVarsAssociatedToModule(appSrvHbeatModule);
            appSrvHbeatModule.Initialize(null);

            // update each environment variable to have a different value
            foreach (var envVarKvp in envVars)
            {
                string newVal = string.Concat(envVarKvp.Value, "_1");
                Environment.SetEnvironmentVariable(envVarKvp.Key, newVal);
            }

            envVars = this.GetEnvVarsAssociatedToModule(appSrvHbeatModule);

            appSrvHbeatModule.UpdateHeartbeatWithAppServiceEnvVarValues();
            foreach (var kvp in appSrvHbeatModule.WebHeartbeatPropertyNameEnvVarMap)
            {
                Assert.True(hbeatProviderMock.HbeatProps.ContainsKey(kvp.Key));
                Assert.Equal(hbeatProviderMock.HbeatProps[kvp.Key], envVars[kvp.Value]);
            }

            this.RemoveTestEnvVarsAssociatedToModule(appSrvHbeatModule);
        }

        [TestMethod]
        public void NoHeartbeatManagerAvailableDoesntThrow()
        {
            var appSrvHbeatModule = new AppServicesHeartbeatTelemetryModule();
            var envVars = this.GetEnvVarsAssociatedToModule(appSrvHbeatModule);

            try
            {
                appSrvHbeatModule.Initialize(null);
            }
            catch (Exception any)
            {
                Assert.False(any == null);
            }

            this.RemoveTestEnvVarsAssociatedToModule(appSrvHbeatModule);
        }

        [TestMethod]
        public void NoAppServicesEnvVarsWorksWithoutFailure()
        {
            var hbeatProviderMock = new HeartbeatProviderMock();
            var appSrvHbeatModule = this.GetAppServiceHeartbeatModuleWithUniqueTestEnvVars(hbeatProviderMock);
            var envVars = this.GetEnvVarsAssociatedToModule(appSrvHbeatModule);

            // ensure all environment variables are set to nothing (remove them from the environment)
            this.RemoveTestEnvVarsAssociatedToModule(appSrvHbeatModule);

            appSrvHbeatModule.UpdateHeartbeatWithAppServiceEnvVarValues();
            foreach (var kvp in appSrvHbeatModule.WebHeartbeatPropertyNameEnvVarMap)
            {
                Assert.Null(hbeatProviderMock.HbeatProps[kvp.Key]);
            }
        }

        /// <summary>
        /// Return a dictionary containing the expected environment variables for the AppServicesHeartbeat module. If
        /// the environment does not contain a value for them, set the environment to have them.
        /// </summary>
        /// <returns>Dictionary with expected environment variable names as the key, current environment variable content as the value.</returns>
        private Dictionary<string, string> GetEnvVarsAssociatedToModule(AppServicesHeartbeatTelemetryModule module = null)
        {
            Dictionary<string, string> envVars = new Dictionary<string, string>();
            var appSrvModule = module ?? new AppServicesHeartbeatTelemetryModule();
            foreach (var kvp in appSrvModule.WebHeartbeatPropertyNameEnvVarMap)
            {
                envVars.Add(kvp.Value, Environment.GetEnvironmentVariable(kvp.Value));
                if (string.IsNullOrEmpty(envVars[kvp.Value]))
                {
                    Environment.SetEnvironmentVariable(kvp.Value, kvp.Key);
                    envVars[kvp.Value] = kvp.Key;
                }
            }

            return envVars;
        }

        private AppServicesHeartbeatTelemetryModule GetAppServiceHeartbeatModuleWithUniqueTestEnvVars(HeartbeatProviderMock hbeatProviderMock = null)
        {
            var appSrvHbeatModule = new AppServicesHeartbeatTelemetryModule(hbeatProviderMock);
            string testSuffix = Guid.NewGuid().ToString();
            for (int i = 0; i < appSrvHbeatModule.WebHeartbeatPropertyNameEnvVarMap.Length; ++i)
            {
                var kvp = appSrvHbeatModule.WebHeartbeatPropertyNameEnvVarMap[i];
                appSrvHbeatModule.WebHeartbeatPropertyNameEnvVarMap[i] = new KeyValuePair<string, string>(kvp.Key, string.Concat(kvp.Value, "_", testSuffix));
            }

            return appSrvHbeatModule;
        }

        private void RemoveTestEnvVarsAssociatedToModule(AppServicesHeartbeatTelemetryModule appSrvHbeatModule)
        {
            foreach (var kvp in appSrvHbeatModule.WebHeartbeatPropertyNameEnvVarMap)
            {
                Environment.SetEnvironmentVariable(kvp.Value, string.Empty);
            }
        }
    }
}
