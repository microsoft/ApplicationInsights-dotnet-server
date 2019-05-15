namespace Microsoft.ApplicationInsights.Tests
{
    using System;
    using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.Implementation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    ///  PerformanceCounterUtilityTests
    /// </summary>
    [TestClass]
    public class PerformanceCounterUtilityTestsCommon
    {
        [TestInitialize]
        public void TestInii()
        {
            Environment.SetEnvironmentVariable("WEBSITE_SITE_NAME", "something");
        }

        [TestMethod]        
        public void GetCollectorReturnsCorrectCollector()
        {
            var actual = PerformanceCounterUtility.GetPerformanceCollector();
        }        
    }
}