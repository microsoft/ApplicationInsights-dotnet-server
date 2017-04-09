namespace Microsoft.ApplicationInsights.Web
{
    using System.Web;

    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Web.Helpers;
    using Microsoft.ApplicationInsights.Web.Implementation;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class HttpContextExtensionTest
    {
        [TestMethod]
        public void GetRequestTelemetryReturnsNullForNullContext()
        {
            Assert.IsNull(HttpContextExtension.GetRequestTelemetry(null));
        }

        [TestMethod]
        public void GetRequestTelemetryReturnsNullIfRequestNotAvailable()
        {
            Assert.IsNull(HttpModuleHelper.GetFakeHttpContext().GetRequestTelemetry());
        }

        [TestMethod]
        public void GetRequestTelemetryReturnsRequestTelemetryFromItems()
        {
            var context = HttpModuleHelper.GetFakeHttpContext();
            var expected = context.SetOperationHolder().Telemetry;
         
            var actual = context.GetRequestTelemetry();

            Assert.AreSame(expected, actual);
        }
    }
}
