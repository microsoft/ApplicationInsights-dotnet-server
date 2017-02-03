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
            var filterInfo = new FilterInfo() { TelemetryType = TelemetryType.Request, FieldName = "Field", Predicate = Predicate.Equals, Comparand = "123", Projection = "Field" };
            var filter = new Filter<TelemetryMock>(filterInfo);
            var telemetry = new TelemetryMock() { Field = "123" };

            // ACT
            object result = filter.Check(telemetry);


            // ASSERT
        }
    }
}
