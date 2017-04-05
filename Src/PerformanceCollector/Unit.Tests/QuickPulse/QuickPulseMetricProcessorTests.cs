namespace Unit.Tests
{
    using System.Collections.Generic;
    using System.Linq;

    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.Filtering;
    using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.Implementation.QuickPulse;
    using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class QuickPulseMetricProcessorTests
    {
        [TestInitialize]
        public void TestInitialize()
        {
            QuickPulseTestHelper.ClearEnvironment();
        }

        [TestMethod]
        public void QuickPulseMetricProcessorCollectsCalculatedMetrics()
        {
            // ARRANGE
            var filterInfo1 = new FilterInfo() { FieldName = "MetricName", Predicate = Predicate.Contains, Comparand = "Awesome" };
            var filterInfo2 = new FilterInfo() { FieldName = "MetricName", Predicate = Predicate.Contains, Comparand = "1" };

            var metrics = new[]
            {
                new CalculatedMetricInfo()
                {
                    Id = "Metric1",
                    TelemetryType = TelemetryType.Metric,
                    Projection = "Value",
                    Aggregation = AggregationType.Avg,
                    FilterGroups = new[] { new FilterConjunctionGroupInfo() { Filters = new[] { filterInfo1, filterInfo2 } } }
                },
                new CalculatedMetricInfo()
                {
                    Id = "Metric2",
                    TelemetryType = TelemetryType.Metric,
                    Projection = "Value",
                    Aggregation = AggregationType.Sum,
                    FilterGroups = new[] { new FilterConjunctionGroupInfo() { Filters = new[] { filterInfo1, filterInfo2 } } }
                }
            };

            CollectionConfigurationError[] errors;
            var collectionConfiguration = new CollectionConfiguration(
                new CollectionConfigurationInfo() { Metrics = metrics },
                out errors,
                new ClockMock());
            var accumulatorManager = new QuickPulseDataAccumulatorManager(collectionConfiguration);
            var metricProcessor = new QuickPulseMetricProcessor();
            var metric = new MetricManager().CreateMetric("Awesome123");

            metricProcessor.StartCollection(accumulatorManager);

            // ACT
            metricProcessor.Track(metric, 1.0d);
            metricProcessor.Track(metric, 2.0d);
            metricProcessor.Track(metric, 3.0d);

            metricProcessor.StopCollection();

            // ASSERT
            Dictionary<string, AccumulatedValue> calculatedMetrics =
                accumulatorManager.CurrentDataAccumulator.CollectionConfigurationAccumulator.MetricAccumulators;

            Assert.AreEqual(2, calculatedMetrics.Count);

            Assert.AreEqual("1, 2, 3", string.Join(", ", calculatedMetrics["Metric1"].Value.Reverse().ToArray()));
            Assert.AreEqual("1, 2, 3", string.Join(", ", calculatedMetrics["Metric2"].Value.Reverse().ToArray()));
        }
    }
}