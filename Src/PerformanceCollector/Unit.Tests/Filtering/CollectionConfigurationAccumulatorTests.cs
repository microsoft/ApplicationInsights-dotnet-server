namespace Unit.Tests
{
    using System;
    using System.Linq;

    using Microsoft.ApplicationInsights.Extensibility.Filtering;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CollectionConfigurationAccumulatorTests
    {
        [TestMethod]
        public void CollectionConfigurationAccumulatorPreparesMetricAccumulatorsTest()
        {
            // ARRANGE
            string[] error;
            var metricInfo = new OperationalizedMetricInfo()
                                 {
                                     SessionId = "Session1",
                                     Id = "Metric1",
                                     TelemetryType = TelemetryType.Request,
                                     Projection = "Name",
                                     Aggregation = AggregationType.Min,
                                     Filters = new FilterInfo[0]
                                 };

            var collectionConfigurationInfo = new CollectionConfigurationInfo() { Metrics = new[] { metricInfo } };
            var collectionConfiguration = new CollectionConfiguration(collectionConfigurationInfo, out error);

            // ACT
            var accumulator = new CollectionConfigurationAccumulator(collectionConfiguration);

            // ASSERT
            Assert.AreSame(collectionConfiguration, accumulator.CollectionConfiguration);
            Assert.IsTrue(Tuple.Create("Session1", "Metric1").Equals(accumulator.MetricAccumulators.Single().Key));
            Assert.AreEqual(AggregationType.Min, accumulator.MetricAccumulators.Single().Value.AggregationType);
        }

        [TestMethod]
        public void CollectionConfigurationAccumulatorPreparesSingleMetricAccumulatorForMetricWithManyIdsTest()
        {
            // ARRANGE
            string[] error;
            var metricInfo1 = new OperationalizedMetricInfo()
                                  {
                                      SessionId = "Session1",
                                      Id = "Metric1",
                                      TelemetryType = TelemetryType.Request,
                                      Projection = "Name",
                                      Aggregation = AggregationType.Min,
                                      Filters = new FilterInfo[0]
                                  };
            var metricInfo2 = new OperationalizedMetricInfo()
                                  {
                                      SessionId = "Session2",
                                      Id = "Metric2",
                                      TelemetryType = TelemetryType.Request,
                                      Projection = "Name",
                                      Aggregation = AggregationType.Min,
                                      Filters = new FilterInfo[0]
                                  };
            var collectionConfigurationInfo = new CollectionConfigurationInfo() { Metrics = new[] { metricInfo1, metricInfo2 } };
            var collectionConfiguration = new CollectionConfiguration(collectionConfigurationInfo, out error);

            // ACT
            var accumulator = new CollectionConfigurationAccumulator(collectionConfiguration);

            // ASSERT
            Assert.AreSame(collectionConfiguration, accumulator.CollectionConfiguration);
            Assert.AreEqual(1, accumulator.MetricAccumulators.Count);
            
            var id1 = accumulator.MetricAccumulators.First().Key;
            var id2 = accumulator.MetricAccumulators.Last().Key;
            var expectedId1 = Tuple.Create("Session1", "Metric1");
            var expectedId2 = Tuple.Create("Session2", "Metric2");

            Assert.IsTrue(id1.Equals(expectedId1) || id1.Equals(expectedId2));
            Assert.IsTrue(id2.Equals(expectedId1) || id2.Equals(expectedId2));
        }
    }
}