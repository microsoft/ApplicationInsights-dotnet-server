namespace DependencyCollector.NetCore.Tests
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class DependencyCollectorDiagnosticListenerTests
    {
        [TestMethod]
        public void TestPlus()
        {
            Assert.AreEqual(4, 3 + 1);
        }
    }
}
