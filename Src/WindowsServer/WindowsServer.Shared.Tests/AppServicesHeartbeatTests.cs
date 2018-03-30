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
    public class AppServicesHeartbeatTests
    {
        [TestMethod]
        public void EnsureInstanceWorksAsIntended()
        {
            using (var appSrvHbeatModule = new AppServicesHeartbeatTelemetryModule())
            {
                Assert.NotNull(AppServicesHeartbeatTelemetryModule.Instance);
            }
            Assert.Null(AppServicesHeartbeatTelemetryModule.Instance);
        }

        [TestMethod]
        public void InitializeIsWorking()
        {
            var envVars = GetEnvVarsAssociatedToModule();
            var hbeatProviderMock = new HeartbeatProviderMock();
            var appSrvHbeatModule = new AppServicesHeartbeatTelemetryModule(hbeatProviderMock);
            appSrvHbeatModule.Initialize(null);

            foreach (var kvp in AppServicesHeartbeatTelemetryModule.WebHeartbeatPropertyNameEnvVarMap)
            {
                Assert.True(hbeatProviderMock.HbeatProps.ContainsKey(kvp.Key));
                Assert.Equal(hbeatProviderMock.HbeatProps[kvp.Key], envVars[kvp.Value]);
            }
        }

        [TestMethod]
        public void UpdateEnvVarsWorksWhenEnvironmentValuesChange()
        {
            var envVars = GetEnvVarsAssociatedToModule();
            var hbeatProviderMock = new HeartbeatProviderMock();
            var appSrvHbeatModule = new AppServicesHeartbeatTelemetryModule(hbeatProviderMock);
            appSrvHbeatModule.Initialize(null);

            // update each environment variable to have a different value
            foreach (var envVarKvp in envVars)
            {
                string newVal = string.Concat(envVarKvp.Value, "_1");
                Environment.SetEnvironmentVariable(envVarKvp.Key, newVal);
            }

            envVars = GetEnvVarsAssociatedToModule();

            Assert.True(appSrvHbeatModule.UpdateHeartbeatWithAppServiceEnvVarValues());
            foreach (var kvp in AppServicesHeartbeatTelemetryModule.WebHeartbeatPropertyNameEnvVarMap)
            {
                Assert.True(hbeatProviderMock.HbeatProps.ContainsKey(kvp.Key));
                Assert.Equal(hbeatProviderMock.HbeatProps[kvp.Key], envVars[kvp.Value]);
            }
        }

        [TestMethod]
        public void NoHeartbeatManagerAvailableDoesntThrow()
        {
            var envVars = GetEnvVarsAssociatedToModule();
            var appSrvHbeatModule = new AppServicesHeartbeatTelemetryModule();
            try
            {
                appSrvHbeatModule.Initialize(null);
            }
            catch (Exception any)
            {
                Assert.False(any == null);
            }
        }

        [TestMethod]
        public void NoAppServicesEnvVarsWorksWithoutFailure()
        {
            var envVars = GetEnvVarsAssociatedToModule();
            // ensure all environment variables are set to nothing (remove them from the environment)
            foreach (var kvp in envVars)
            {
                Environment.SetEnvironmentVariable(kvp.Key, string.Empty);
            }
            var hbeatProviderMock = new HeartbeatProviderMock();
            var appSrvHbeatModule = new AppServicesHeartbeatTelemetryModule(hbeatProviderMock);
            Assert.True(appSrvHbeatModule.UpdateHeartbeatWithAppServiceEnvVarValues());
            foreach (var kvp in AppServicesHeartbeatTelemetryModule.WebHeartbeatPropertyNameEnvVarMap)
            {
                Assert.False(hbeatProviderMock.HbeatProps.ContainsKey(kvp.Key));
            }
        }

        /// <summary>
        /// Return a dictionary containing the expected environment variables for the AppServicesHeartbeat module. If
        /// the environment does not contain a value for them, set the environment to have them.
        /// </summary>
        /// <returns>Dictionary with expected environment variable names as the key, current environment variable content as the value.</returns>
        private Dictionary<string,string> GetEnvVarsAssociatedToModule()
        {
            Dictionary<string, string> envVars = new Dictionary<string, string>();
            foreach (var kvp in AppServicesHeartbeatTelemetryModule.WebHeartbeatPropertyNameEnvVarMap)
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
    }
}
