namespace Microsoft.ApplicationInsights.WindowsServer
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Net.Http;
    using System.Runtime.Serialization.Json;
    using System.Text;
    using System.Threading.Tasks;
    using Microsoft.ApplicationInsights.WindowsServer.Implementation;
    using Microsoft.ApplicationInsights.WindowsServer.Implementation.DataContracts;
    using Microsoft.ApplicationInsights.WindowsServer.Mock;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Assert = Xunit.Assert;

    [TestClass]
    public class AppServiceEnvVarMonitorTests
    {
        // used to clean up the environment variables after we've run this test
        private static Dictionary<string, string> environmentInitialState;

        [ClassInitialize]
        public static void InitializeTests(TestContext context)
        {
            environmentInitialState = GetCurrentAppServiceEnvironmentVariableValues(false);
        }

        [ClassCleanup]
        public static void CleanupTests()
        {
            foreach (var kvp in environmentInitialState)
            {
                Environment.SetEnvironmentVariable(kvp.Key, kvp.Value);
            }

            environmentInitialState = null;
        }

        [TestMethod]
        public void ConfirmIntervalCheckEnforced()
        {
            var envVars = GetCurrentAppServiceEnvironmentVariableValues();

            foreach (var kvp in envVars)
            {
                string val = string.Empty;
                AppServiceEnvVarMonitor.GetUpdatedEnvironmentVariable(kvp.Key, ref val);

                // set the value to something new
                Environment.SetEnvironmentVariable(kvp.Key, string.Concat("UPDATED-", val, "-UPDATED"));
            }

            // set the next-check time to a value that won't get hit
            AppServiceEnvVarMonitor.NextCheckTime = DateTime.MaxValue;

            // ensure the current values are indeed different
            var currentEnvVars = GetCurrentAppServiceEnvironmentVariableValues();

            // ensure the values are cached and aren't getting re-read at this time
            foreach (var kvp in envVars)
            {
                string cachedVal = string.Empty;
                AppServiceEnvVarMonitor.GetUpdatedEnvironmentVariable(kvp.Key, ref cachedVal);
                Assert.Equal(kvp.Value, cachedVal, StringComparer.Ordinal);
                Assert.NotEqual(cachedVal, currentEnvVars[kvp.Key], StringComparer.Ordinal);
            }
        }

        [TestMethod]
        public void ConfirmUpdatedEnvironmentIsCaptured()
        {
            var envVars = GetCurrentAppServiceEnvironmentVariableValues();

            foreach (var kvp in envVars)
            {
                string val = string.Empty;
                AppServiceEnvVarMonitor.GetUpdatedEnvironmentVariable(kvp.Key, ref val);
                
                // set the value to something new
                Environment.SetEnvironmentVariable(kvp.Key, string.Concat("UPDATED-", val, "-UPDATED"));
            }

            // set the next-check time to a value that will re-read the values immediately
            AppServiceEnvVarMonitor.NextCheckTime = DateTime.MinValue;

            // ensure the current values are indeed different
            var currentEnvVars = GetCurrentAppServiceEnvironmentVariableValues();

            // ensure the values are re-read
            foreach (var kvp in envVars)
            {
                string cachedVal = string.Empty;
                AppServiceEnvVarMonitor.GetUpdatedEnvironmentVariable(kvp.Key, ref cachedVal);
                Assert.Equal(currentEnvVars[kvp.Key], cachedVal, StringComparer.Ordinal);
                Assert.NotEqual(cachedVal, kvp.Value, StringComparer.Ordinal);
            }
        }

        private static Dictionary<string, string> GetCurrentAppServiceEnvironmentVariableValues(bool supplyValue = true)
        {
            int testValueCount = 0;
            Dictionary<string, string> envVars = new Dictionary<string, string>();

            foreach (var kvp in AppServiceEnvVarMonitor.CheckedValues)
            {
                string envVar = Environment.GetEnvironmentVariable(kvp.Key);
                if (supplyValue && string.IsNullOrEmpty(envVar))
                {
                    envVar = $"{testValueCount}_Stand-inValue_{testValueCount}";
                    testValueCount++;
                    Environment.SetEnvironmentVariable(kvp.Key, envVar);
                }

                envVars.Add(kvp.Key, envVar);
            }

            return envVars;
        }
    }
}
