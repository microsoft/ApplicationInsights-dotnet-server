// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.ApplicationInsights.DependencyCollector
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System;

    [TestClass]
    public class InstrumentationKeyHashLookupHelperTests
    {
        [TestMethod]
        public void GetInstrumentationKeyHashWithNull()
        {
            Assert.ThrowsException<ArgumentNullException>(() => InstrumentationKeyHashLookupHelper.GetInstrumentationKeyHash(null));
        }

        [TestMethod]
        public void GetInstrumentationKeyHashWithEmpty()
        {
            Assert.ThrowsException<ArgumentNullException>(() => InstrumentationKeyHashLookupHelper.GetInstrumentationKeyHash(""));
        }

        [TestMethod]
        public void GetInstrumentationKeyHashWithWhitespace()
        {
            GetInstrumentationKeyHashTest(" ", "Nqnn8clbgv+5l0PgxcTOldg8mkMKrFn4TvPL+rYUUGg=");
        }

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