namespace Unit.Tests
{
    using Microsoft.ApplicationInsights.Extensibility.Filtering;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class OperationalizedMetricInfoTests
    {
        [TestMethod]
        public void OperationalizedMetricInfoEqualityAccountsForTelemetryTypeTest()
        {
            // ARRANGE
            var metricInfo1 = new OperationalizedMetricInfo()
                                  {
                                      SessionId = "Session1",
                                      Id = "Metric1",
                                      TelemetryType = TelemetryType.Request,
                                      Projection = "Name",
                                      Aggregation = AggregationType.Avg,
                                      Filters = new FilterInfo[0]
                                  };
            var metricInfo2 = new OperationalizedMetricInfo()
                                  {
                                      SessionId = "Session1",
                                      Id = "Metric1",
                                      TelemetryType = TelemetryType.Request,
                                      Projection = "Name",
                                      Aggregation = AggregationType.Avg,
                                      Filters = new FilterInfo[0]
                                  };
            var metricInfo3 = new OperationalizedMetricInfo()
                                  {
                                      SessionId = "Session1",
                                      Id = "Metric1",
                                      TelemetryType = TelemetryType.Dependency,
                                      Projection = "Name",
                                      Aggregation = AggregationType.Avg,
                                      Filters = new FilterInfo[0]
                                  };

            // ACT
            bool equalityCheck = metricInfo1.Equals(metricInfo2) && metricInfo1 == metricInfo2;
            bool inequalityCheck = !metricInfo1.Equals(metricInfo3) && !(metricInfo1 == metricInfo3);

            // ASSERT
            Assert.IsTrue(equalityCheck);
            Assert.IsTrue(inequalityCheck);
        }

        [TestMethod]
        public void OperationalizedMetricInfoEqualityAccountsForProjectionTest()
        {
            // ARRANGE
            var metricInfo1 = new OperationalizedMetricInfo()
            {
                SessionId = "Session1",
                Id = "Metric1",
                TelemetryType = TelemetryType.Request,
                Projection = "Name",
                Aggregation = AggregationType.Avg,
                Filters = new FilterInfo[0]
            };
            var metricInfo2 = new OperationalizedMetricInfo()
            {
                SessionId = "Session1",
                Id = "Metric1",
                TelemetryType = TelemetryType.Request,
                Projection = "Name",
                Aggregation = AggregationType.Avg,
                Filters = new FilterInfo[0]
            };
            var metricInfo3 = new OperationalizedMetricInfo()
            {
                SessionId = "Session1",
                Id = "Metric1",
                TelemetryType = TelemetryType.Request,
                Projection = "Name1",
                Aggregation = AggregationType.Avg,
                Filters = new FilterInfo[0]
            };

            // ACT
            bool equalityCheck = metricInfo1.Equals(metricInfo2) && metricInfo1 == metricInfo2;
            bool inequalityCheck = !metricInfo1.Equals(metricInfo3) && !(metricInfo1 == metricInfo3);

            // ASSERT
            Assert.IsTrue(equalityCheck);
            Assert.IsTrue(inequalityCheck);
        }

        [TestMethod]
        public void OperationalizedMetricInfoEqualityAccountsForAggregationTest()
        {
            // ARRANGE
            var metricInfo1 = new OperationalizedMetricInfo()
            {
                SessionId = "Session1",
                Id = "Metric1",
                TelemetryType = TelemetryType.Request,
                Projection = "Name",
                Aggregation = AggregationType.Avg,
                Filters = new FilterInfo[0]
            };
            var metricInfo2 = new OperationalizedMetricInfo()
            {
                SessionId = "Session1",
                Id = "Metric1",
                TelemetryType = TelemetryType.Request,
                Projection = "Name",
                Aggregation = AggregationType.Avg,
                Filters = new FilterInfo[0]
            };
            var metricInfo3 = new OperationalizedMetricInfo()
            {
                SessionId = "Session1",
                Id = "Metric1",
                TelemetryType = TelemetryType.Request,
                Projection = "Name",
                Aggregation = AggregationType.Max,
                Filters = new FilterInfo[0]
            };

            // ACT
            bool equalityCheck = metricInfo1.Equals(metricInfo2) && metricInfo1 == metricInfo2;
            bool inequalityCheck = !metricInfo1.Equals(metricInfo3) && !(metricInfo1 == metricInfo3);

            // ASSERT
            Assert.IsTrue(equalityCheck);
            Assert.IsTrue(inequalityCheck);
        }

        [TestMethod]
        public void OperationalizedMetricInfoEqualityAccountsForFiltersTest()
        {
            // ARRANGE
            var metricInfo1 = new OperationalizedMetricInfo()
                                  {
                                      SessionId = "Session1",
                                      Id = "Metric1",
                                      TelemetryType = TelemetryType.Request,
                                      Projection = "Name",
                                      Aggregation = AggregationType.Avg,
                                      Filters = new[] { new FilterInfo() { FieldName = "FieldName", Comparand = "Comparand", Predicate = Predicate.Equal } }
                                  };
            var metricInfo2 = new OperationalizedMetricInfo()
            {
                SessionId = "Session1",
                Id = "Metric1",
                TelemetryType = TelemetryType.Request,
                Projection = "Name",
                Aggregation = AggregationType.Avg,
                Filters = new[] { new FilterInfo() { FieldName = "FieldName", Comparand = "Comparand", Predicate = Predicate.Equal } }
            };
            var metricInfo3 = new OperationalizedMetricInfo()
            {
                SessionId = "Session1",
                Id = "Metric1",
                TelemetryType = TelemetryType.Request,
                Projection = "Name",
                Aggregation = AggregationType.Avg,
                Filters = new[] { new FilterInfo() { FieldName = "FieldName1", Comparand = "Comparand", Predicate = Predicate.Equal } }
            };

            // ACT
            bool equalityCheck = metricInfo1.Equals(metricInfo2) && metricInfo1 == metricInfo2;
            bool inequalityCheck = !metricInfo1.Equals(metricInfo3) && !(metricInfo1 == metricInfo3);

            // ASSERT
            Assert.IsTrue(equalityCheck);
            Assert.IsTrue(inequalityCheck);
        }

        [TestMethod]
        public void OperationalizedMetricInfoEqualityDoesNotAccountForSessionIdTest()
        {
            // ARRANGE
            var metricInfo1 = new OperationalizedMetricInfo()
            {
                SessionId = "Session1",
                Id = "Metric1",
                TelemetryType = TelemetryType.Request,
                Projection = "Name",
                Aggregation = AggregationType.Avg,
                Filters = new FilterInfo[0]
            };
            var metricInfo2 = new OperationalizedMetricInfo()
            {
                SessionId = "Session1",
                Id = "Metric1",
                TelemetryType = TelemetryType.Request,
                Projection = "Name",
                Aggregation = AggregationType.Avg,
                Filters = new FilterInfo[0]
            };
            var metricInfo3 = new OperationalizedMetricInfo()
            {
                SessionId = "Session2",
                Id = "Metric1",
                TelemetryType = TelemetryType.Request,
                Projection = "Name",
                Aggregation = AggregationType.Avg,
                Filters = new FilterInfo[0]
            };

            // ACT
            bool equalityCheck1 = metricInfo1.Equals(metricInfo2) && metricInfo1 == metricInfo2;
            bool equalityCheck2 = metricInfo1.Equals(metricInfo3) && metricInfo1 == metricInfo3;

            // ASSERT
            Assert.IsTrue(equalityCheck1);
            Assert.IsTrue(equalityCheck2);
        }

        [TestMethod]
        public void OperationalizedMetricInfoEqualityDoesNotAccountForIdTest()
        {
            // ARRANGE
            var metricInfo1 = new OperationalizedMetricInfo()
            {
                SessionId = "Session1",
                Id = "Metric1",
                TelemetryType = TelemetryType.Request,
                Projection = "Name",
                Aggregation = AggregationType.Avg,
                Filters = new FilterInfo[0]
            };
            var metricInfo2 = new OperationalizedMetricInfo()
            {
                SessionId = "Session1",
                Id = "Metric1",
                TelemetryType = TelemetryType.Request,
                Projection = "Name",
                Aggregation = AggregationType.Avg,
                Filters = new FilterInfo[0]
            };
            var metricInfo3 = new OperationalizedMetricInfo()
            {
                SessionId = "Session1",
                Id = "Metric2",
                TelemetryType = TelemetryType.Request,
                Projection = "Name",
                Aggregation = AggregationType.Avg,
                Filters = new FilterInfo[0]
            };

            // ACT
            bool equalityCheck1 = metricInfo1.Equals(metricInfo2) && metricInfo1 == metricInfo2;
            bool equalityCheck2 = metricInfo1.Equals(metricInfo3) && metricInfo1 == metricInfo3;

            // ASSERT
            Assert.IsTrue(equalityCheck1);
            Assert.IsTrue(equalityCheck2);
        }
    }
}