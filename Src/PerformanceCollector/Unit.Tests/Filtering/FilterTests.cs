namespace Unit.Tests
{
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility.Filtering;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Filter tests.
    /// </summary>
    [TestClass]
    public class FilterTests
    {
        [TestMethod]
        public void FilterSanityTest()
        {
            // ARRANGE
            var filterInfo = new FilterInfo() { FieldName = "Sequence", Predicate = Predicate.Equals, Comparand = "123", Projection = "Field" };
            var filter = new Filter(filterInfo, typeof(RequestTelemetry));
            var telemetry = new RequestTelemetry() { Sequence= "123" };

            // ACT
            object result = filter.Check(telemetry);


            // ASSERT
        }
    }
}
