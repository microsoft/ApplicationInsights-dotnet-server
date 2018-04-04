namespace Microsoft.ApplicationInsights.WindowsServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.ApplicationInsights.WindowsServer.Implementation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Assert = Xunit.Assert;

    [TestClass]
    public class EnvironmentVariableMonitorTests
    {
        [TestMethod]
        public void EnsureInstanceWorksAsIntended()
        {
            Assert.NotNull(AppServiceEnvironmentVariableMonitor.Instance);
        }

        [TestMethod]
        public void EnsureEnvironmentVariablesAreCapturedImmediately()
        {
            var envVars = GetCurrentAppServiceEnvironmentVariableValues();
            var envMonitor = new Mock.MockEnvironmentVariableMonitor(envVars.Keys);
            foreach (var kvp in envVars)
            {
                string cachedVal = string.Empty;
                envMonitor.GetCurrentEnvironmentVariableValue(kvp.Key, ref cachedVal);
                Assert.Equal(kvp.Value, cachedVal, StringComparer.Ordinal);
            }
        }

        [TestMethod]
        public void ConfirmUpdatedEnvironmentIsNotDetectedPriorToUpdate()
        {
            var envVars = GetCurrentAppServiceEnvironmentVariableValues();
            var envMonitor = new Mock.MockEnvironmentVariableMonitor(envVars.Keys);
            foreach (var kvp in envVars)
            {
                string updatedValue = Guid.NewGuid().ToString();
                Assert.NotEqual(kvp.Value, updatedValue, StringComparer.Ordinal);

                Environment.SetEnvironmentVariable(kvp.Key, updatedValue);

                string cachedValue = string.Empty;
                envMonitor.GetCurrentEnvironmentVariableValue(kvp.Key, ref cachedValue);
                Assert.Equal(kvp.Value, cachedValue, StringComparer.Ordinal);
            }
        }

        [TestMethod]
        public void ConfirmUpdatedEnvironmentIsDetectedPostUpdate()
        {
            var envVars = GetCurrentAppServiceEnvironmentVariableValues();
            var envMonitor = new Mock.MockEnvironmentVariableMonitor(envVars.Keys);
            var updatedVars = new Dictionary<string, string>();

            foreach (var kvp in envVars)
            {
                string updatedValue = Guid.NewGuid().ToString();
                Assert.NotEqual(kvp.Value, updatedValue, StringComparer.Ordinal);

                Environment.SetEnvironmentVariable(kvp.Key, updatedValue);
                updatedVars.Add(kvp.Key, updatedValue);
            }

            envMonitor.PerformCheckForUpdatedVariables();
            Assert.True(envMonitor.DetectedUpdatedVarValue);

            foreach (var kvp in envVars)
            {
                string cachedValue = string.Empty;
                envMonitor.GetCurrentEnvironmentVariableValue(kvp.Key, ref cachedValue);

                Assert.Equal(updatedVars[kvp.Key], cachedValue, StringComparer.Ordinal);
                Assert.NotEqual(kvp.Value, cachedValue, StringComparer.Ordinal);
            }
        }

        /// <summary>
        /// Create a set of environment variables that mimics the default values used by
        /// the AppServiceEnvVarMonitor, and a set of values for them. Each time this method
        /// is called the names and values of the environment variables will be unique as a Guid
        /// is used.
        /// </summary>
        /// <returns>Dictionary containing the environment variable names and their current values.</returns>
        private static Dictionary<string, string> GetCurrentAppServiceEnvironmentVariableValues()
        {
            int testValueCount = 0;
            Dictionary<string, string> envVars = new Dictionary<string, string>();

            string testVarSuffix = Guid.NewGuid().ToString();
            foreach (string envVarName in AppServiceEnvironmentVariableMonitor.PreloadedMonitoredEnvironmentVariables)
            {
                string testVarName = string.Concat(envVarName, "_", testVarSuffix);
                string testVarValue = $"{testValueCount}_Stand-inValue_{testVarSuffix}_{testValueCount}";
                testValueCount++;
                Environment.SetEnvironmentVariable(testVarName, testVarValue);

                envVars.Add(testVarName, testVarValue);
            }

            return envVars;
        }
    }
}
