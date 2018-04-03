namespace Microsoft.ApplicationInsights.WindowsServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.ApplicationInsights.WindowsServer.Implementation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Assert = Xunit.Assert;

    [TestClass]
    public class AppServiceEnvVarMonitorTests
    {
        [TestMethod]
        public void ConfirmIntervalCheckEnforced()
        {
            var envVars = GetCurrentAppServiceEnvironmentVariableValues();
            var envMonitor = new AppServiceEnvVarMonitor(envVars.Keys.ToList());

            // set the next-check time to a value that won't get hit
            envMonitor.NextCheckTime = DateTime.MaxValue;

            foreach (var kvp in envVars)
            {
                string val = string.Empty;
                envMonitor.GetUpdatedEnvironmentVariable(kvp.Key, ref val);

                // set the value to something new
                Environment.SetEnvironmentVariable(kvp.Key, string.Concat("UPDATED-", val, "-UPDATED"));
            }

            // ensure the values are cached and aren't getting re-read at this time
            foreach (var kvp in envVars)
            {
                string cachedVal = string.Empty;
                envMonitor.GetUpdatedEnvironmentVariable(kvp.Key, ref cachedVal);
                Assert.Equal(kvp.Value, cachedVal, StringComparer.Ordinal);
                Assert.NotEqual(cachedVal, Environment.GetEnvironmentVariable(kvp.Key), StringComparer.Ordinal);
            }
        }

        [TestMethod]
        public void ConfirmUpdatedEnvironmentIsCaptured()
        {
            var envVars = GetCurrentAppServiceEnvironmentVariableValues();
            var envMonitor = new AppServiceEnvVarMonitor(envVars.Keys.ToList());

            foreach (var kvp in envVars)
            {
                string val = string.Empty;
                envMonitor.GetUpdatedEnvironmentVariable(kvp.Key, ref val);
                
                // set the value to something new
                Environment.SetEnvironmentVariable(kvp.Key, string.Concat("UPDATED-", val, "-UPDATED"));
            }

            // set the next-check time to a value that will re-read the values immediately
            envMonitor.NextCheckTime = DateTime.MinValue;

            // ensure the values are re-read
            foreach (var kvp in envVars)
            {
                string cachedVal = string.Empty;
                envMonitor.GetUpdatedEnvironmentVariable(kvp.Key, ref cachedVal);
                Assert.Equal(Environment.GetEnvironmentVariable(kvp.Key), cachedVal, StringComparer.Ordinal);
                Assert.NotEqual(cachedVal, kvp.Value, StringComparer.Ordinal);
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
            foreach (string envVarName in AppServiceEnvVarMonitor.DefaultEnvVars)
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
