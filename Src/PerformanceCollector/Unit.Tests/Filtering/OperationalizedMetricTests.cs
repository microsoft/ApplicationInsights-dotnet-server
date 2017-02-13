namespace Unit.Tests
{
    using System;
    using System.Linq;

    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility.Filtering;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class OperationalizedMetricTests
    {
        [TestMethod]
        public void OperationalizedMetricFiltersCorrectlyTest()
        {
            // ARRANGE
            var filterInfo1 = new FilterInfo() { FieldName = "Name", Predicate = Predicate.Contains, Comparand = "dog" };
            var filterInfo2 = new FilterInfo() { FieldName = "Name", Predicate = Predicate.Contains, Comparand = "cat" };
            var metricInfo = new OperationalizedMetricInfo()
                                 {
                                     SessionId = "Session1",
                                     Id = "Metric1",
                                     TelemetryType = TelemetryType.Request,
                                     Projection = "Name",
                                     Aggregation = AggregationType.Sum,
                                     Filters = new[] { filterInfo1, filterInfo2 }
                                 };

            var telemetryThatMustPass = new RequestTelemetry() { Name = "Both the words 'dog' and 'CAT' are here, which satisfies both filters" };
            var telemetryThatMustFail1 = new RequestTelemetry() { Name = "This value only contains the word 'dog', but not the other one" };
            var telemetryThatMustFail2 = new RequestTelemetry() { Name = "This value only contains the word 'cat', but not the other one" };

            // ACT
            string[] errors;
            var metric = new OperationalizedMetric<RequestTelemetry>(metricInfo, out errors);

            // ASSERT
            Assert.AreEqual(0, errors.Length);

            Assert.IsTrue(metric.CheckFilters(telemetryThatMustPass, out errors));
            Assert.AreEqual(0, errors.Length);

            Assert.IsFalse(metric.CheckFilters(telemetryThatMustFail1, out errors));
            Assert.AreEqual(0, errors.Length);

            Assert.IsFalse(metric.CheckFilters(telemetryThatMustFail2, out errors));
            Assert.AreEqual(0, errors.Length);
        }

        [TestMethod]
        public void OperationalizedMetricProjectsCorrectlyTest()
        {
            // ARRANGE
            var metricInfo = new OperationalizedMetricInfo()
            {
                SessionId = "Session1",
                Id = "Metric1",
                TelemetryType = TelemetryType.Request,
                Projection = "Id",
                Aggregation = AggregationType.Sum,
                Filters = new FilterInfo[0]
            };

            var telemetry = new RequestTelemetry() { Name = "1.23", Id = "5.67" };
            
            // ACT
            string[] errors;
            var metric = new OperationalizedMetric<RequestTelemetry>(metricInfo, out errors);
            double projection = metric.Project(telemetry);

            // ASSERT
            Assert.AreEqual(AggregationType.Sum, metric.AggregationType);
            Assert.AreEqual(0, errors.Length);
            Assert.AreEqual(5.67d, projection);
        }

        [TestMethod]
        public void OperationalizedMetricAggregatesCorrectlyTest()
        {
            // ARRANGE
            double[] accumulatedValues = { 1d, 3d };

            // ACT
            double avg = OperationalizedMetric<object>.Aggregate(accumulatedValues, AggregationType.Avg);
            double sum = OperationalizedMetric<object>.Aggregate(accumulatedValues, AggregationType.Sum);
            double min = OperationalizedMetric<object>.Aggregate(accumulatedValues, AggregationType.Min);
            double max = OperationalizedMetric<object>.Aggregate(accumulatedValues, AggregationType.Max);

            // ASSERT
            Assert.AreEqual(2d, avg);
            Assert.AreEqual(4d, sum);
            Assert.AreEqual(1d, min);
            Assert.AreEqual(3d, max);
        }

        [TestMethod]
        public void OperationalizedMetricAggregatesCorrectlyForEmptyDataSetTest()
        {
            // ARRANGE
            double[] accumulatedValues = { };

            // ACT
            double avg = OperationalizedMetric<object>.Aggregate(accumulatedValues, AggregationType.Avg);
            double sum = OperationalizedMetric<object>.Aggregate(accumulatedValues, AggregationType.Sum);
            double min = OperationalizedMetric<object>.Aggregate(accumulatedValues, AggregationType.Min);
            double max = OperationalizedMetric<object>.Aggregate(accumulatedValues, AggregationType.Max);

            // ASSERT
            Assert.AreEqual(0d, avg);
            Assert.AreEqual(0d, sum);
            Assert.AreEqual(0d, min);
            Assert.AreEqual(0d, max);
        }

        [TestMethod]
        public void OperationalizedMetricReportsErrorsForInvalidFiltersTest()
        {
            // ARRANGE
            var filterInfo1 = new FilterInfo() { FieldName = "Name", Predicate = Predicate.Equal, Comparand = "Sky" };
            var filterInfo2 = new FilterInfo() { FieldName = "NonExistentField", Predicate = Predicate.Equal, Comparand = "Comparand" };
            var metricInfo = new OperationalizedMetricInfo()
                                 {
                                     SessionId = "Session1",
                                     Id = "Metric1",
                                     TelemetryType = TelemetryType.Request,
                                     Projection = "Name",
                                     Aggregation = AggregationType.Avg,
                                     Filters = new[] { filterInfo1, filterInfo2 }
                                 };

            // ACT
            string[] errors;
            var metric = new OperationalizedMetric<RequestTelemetry>(metricInfo, out errors);

            // ASSERT
            Assert.IsTrue(errors.Single().Contains("NonExistentField"));

            // we must be left with the one valid filter only
            Assert.IsTrue(metric.CheckFilters(new RequestTelemetry() { Name = "sky" }, out errors));
            Assert.AreEqual(0, errors.Length);

            Assert.IsFalse(metric.CheckFilters(new RequestTelemetry() { Name = "sky1" }, out errors));
            Assert.AreEqual(0, errors.Length);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void OperationalizedMetricThrowsWhenInvalidProjectionTest()
        {
            // ARRANGE
            var metricInfo = new OperationalizedMetricInfo()
                                 {
                                     SessionId = "Session1",
                                     Id = "Metric1",
                                     TelemetryType = TelemetryType.Request,
                                     Projection = "NonExistentFieldName",
                                     Aggregation = AggregationType.Sum,
                                     Filters = new FilterInfo[0]
                                 };

            // ACT
            string[] errors;
            new OperationalizedMetric<RequestTelemetry>(metricInfo, out errors);

            // ASSERT
        }

        [TestMethod]
        public void OperationalizedMetricReportsErrorWhenProjectionIsNotDoubleTest()
        {
            // ARRANGE
            var metricInfo = new OperationalizedMetricInfo()
            {
                SessionId = "Session1",
                Id = "Metric1",
                TelemetryType = TelemetryType.Request,
                Projection = "Id",
                Aggregation = AggregationType.Sum,
                Filters = new FilterInfo[0]
            };

            var telemetry = new RequestTelemetry() { Id = "NotDoubleValue" };

            string[] errors;
            var metric = new OperationalizedMetric<RequestTelemetry>(metricInfo, out errors);

            // ACT
            try
            {
                metric.Project(telemetry);
            }
            catch (ArgumentOutOfRangeException e)
            {
                // ASSERT
                Assert.IsTrue(e.ToString().Contains("Id"));
                return;
            }

            Assert.Fail();
        }
    }
}