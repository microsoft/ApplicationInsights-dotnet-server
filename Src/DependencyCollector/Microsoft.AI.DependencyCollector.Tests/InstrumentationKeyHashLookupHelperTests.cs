namespace Microsoft.ApplicationInsights.DependencyCollector
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;

    /// <summary>
    /// Unit tests for InstrumentationKeyHashLookupHelper.
    /// </summary>
    [TestClass]
    public class InstrumentationKeyHashLookupHelperTests
    {
        /// <summary>
        /// Call GetInstrumentationKeyHas() with null.
        /// </summary>
        [TestMethod]
        public void GetInstrumentationKeyHashWithNull()
        {
            Assert.ThrowsException<ArgumentNullException>(() => InstrumentationKeyHashLookupHelper.GetInstrumentationKeyHash(null));
        }

        /// <summary>
        /// Call GetInstrumentationKeyHas() with an empty string.
        /// </summary>
        [TestMethod]
        public void GetInstrumentationKeyHashWithEmpty()
        {
            Assert.ThrowsException<ArgumentNullException>(() => InstrumentationKeyHashLookupHelper.GetInstrumentationKeyHash(""));
        }

        /// <summary>
        /// Call GetInstrumentationKeyHas() with whitespace.
        /// </summary>
        [TestMethod]
        public void GetInstrumentationKeyHashWithWhitespace()
        {
            GetInstrumentationKeyHashTest(" ", "Nqnn8clbgv+5l0PgxcTOldg8mkMKrFn4TvPL+rYUUGg=");
        }

        /// <summary>
        /// Call GetInstrumentationKeyHas() with a normal value.
        /// </summary>
        [TestMethod]
        public void GetInstrumentationKeyHash()
        {
            GetInstrumentationKeyHashTest("MOCK-INSTRUMENTATION-KEY", "XkmKJQafRIm8aWF3cLYFlBVEDDhOhR8WDEsjlCdUVGE=");
        }

        private static void GetInstrumentationKeyHashTest(string instrumentationKey, string expectedHash)
        {
            Assert.AreEqual(expectedHash, InstrumentationKeyHashLookupHelper.GetInstrumentationKeyHash(instrumentationKey));

            // Run the function again to make sure that the cached version is the same as the original.
            Assert.AreEqual(expectedHash, InstrumentationKeyHashLookupHelper.GetInstrumentationKeyHash(instrumentationKey));
        }
    }
}