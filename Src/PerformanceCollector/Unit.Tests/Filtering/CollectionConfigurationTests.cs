namespace Unit.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.ExceptionServices;

    using Microsoft.ApplicationInsights.Extensibility.Filtering;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CollectionConfigurationTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void CollectionConfigurationThrowsOnNullInputTest()
        {
            // ARRANGE
            
            // ACT
            string[] errors;
            new CollectionConfiguration(null, out errors);

            // ASSERT
        }

        [TestMethod]
        public void CollectionConfigurationCreatesMetricsTest()
        {
            // ARRANGE
            string[] errors;
            var filters = new[] { new FilterInfo() { FieldName = "Name", Predicate = Predicate.Equal, Comparand = "Request1" } };
            var metrics = new[]
                              {
                                  new OperationalizedMetricInfo()
                                      {
                                          SessionId = "Session0",
                                          Id = "Metric0",
                                          TelemetryType = TelemetryType.Request,
                                          Projection = "Name",
                                          Aggregation = AggregationType.Avg,
                                          Filters = filters
                                      },
                                  new OperationalizedMetricInfo()
                                      {
                                          SessionId = "Session1",
                                          Id = "Metric1",
                                          TelemetryType = TelemetryType.Request,
                                          Projection = "Id",
                                          Aggregation = AggregationType.Sum,
                                          Filters = filters
                                      },
                                  new OperationalizedMetricInfo()
                                      {
                                          SessionId = "Session2",
                                          Id = "Metric2",
                                          TelemetryType = TelemetryType.Dependency,
                                          Projection = "Name",
                                          Aggregation = AggregationType.Avg,
                                          Filters = filters
                                      },
                                  new OperationalizedMetricInfo()
                                      {
                                          SessionId = "Session3",
                                          Id = "Metric3",
                                          TelemetryType = TelemetryType.Exception,
                                          Projection = "Message",
                                          Aggregation = AggregationType.Avg,
                                          Filters = filters
                                      },
                                   new OperationalizedMetricInfo()
                                      {
                                          SessionId = "Session4",
                                          Id = "Metric4",
                                          TelemetryType = TelemetryType.Event,
                                          Projection = "Name",
                                          Aggregation = AggregationType.Avg,
                                          Filters = filters
                                      },
                                    new OperationalizedMetricInfo()
                                      {
                                          SessionId = "Session4",
                                          Id = "Metric5",
                                          TelemetryType = TelemetryType.Metric,
                                          Projection = "Value",
                                          Aggregation = AggregationType.Avg,
                                          Filters = filters
                                      }
                              };

            // ACT
            var collectionConfiguration = new CollectionConfiguration(new CollectionConfigurationInfo() { Metrics = metrics }, out errors);

            // ASSERT
            Assert.AreEqual(Tuple.Create("Session0", "Metric0"), collectionConfiguration.RequestMetrics.First().IdsToReportUnder.Single());
            Assert.AreEqual(Tuple.Create("Session1", "Metric1"), collectionConfiguration.RequestMetrics.Last().IdsToReportUnder.Single());
            Assert.AreEqual(Tuple.Create("Session2", "Metric2"), collectionConfiguration.DependencyMetrics.Single().IdsToReportUnder.Single());
            Assert.AreEqual(Tuple.Create("Session3", "Metric3"), collectionConfiguration.ExceptionMetrics.Single().IdsToReportUnder.Single());
            Assert.AreEqual(Tuple.Create("Session4", "Metric4"), collectionConfiguration.EventMetrics.Single().IdsToReportUnder.Single());
            Assert.AreEqual(Tuple.Create("Session4", "Metric5"), collectionConfiguration.MetricMetrics.Single().IdsToReportUnder.Single());

            Assert.AreEqual(5, collectionConfiguration.TelemetryMetadata.Count());
            Assert.IsTrue(collectionConfiguration.TelemetryMetadata.All(ids => ids.Item1.Count == 1));
            Assert.IsTrue(collectionConfiguration.MetricMetrics.Single().IdsToReportUnder.Single().Equals(Tuple.Create("Session4", "Metric5")));
        }

        [TestMethod]
        public void CollectionConfigurationMergesMetricsCorrectlyTest()
        {
            // ARRANGE
            string[] errors;
            var filter1 = new FilterInfo() { FieldName = "Name", Predicate = Predicate.Equal, Comparand = "Request1" };
            var filter2 = new FilterInfo() { FieldName = "Name", Predicate = Predicate.Equal, Comparand = "Request1" };
            var metrics = new[]
                              {
                                  new OperationalizedMetricInfo()
                                      {
                                          SessionId = "Session1",
                                          Id = "Metric1",
                                          TelemetryType = TelemetryType.Request,
                                          Projection = "Name",
                                          Aggregation = AggregationType.Avg,
                                          Filters = new[] { filter1 }
                                      },
                                  new OperationalizedMetricInfo()
                                      {
                                          SessionId = "Session2",
                                          Id = "Metric2",
                                          TelemetryType = TelemetryType.Request,
                                          Projection = "Name",
                                          Aggregation = AggregationType.Avg,
                                          Filters = new[] { filter2 }
                                      }
                              };

            // ACT
            var collectionConfiguration = new CollectionConfiguration(new CollectionConfigurationInfo() { Metrics = metrics }, out errors);

            // ASSERT
            Assert.AreEqual(1, collectionConfiguration.RequestMetrics.Count());

            var expectedIds = new MetricIdCollection(new[] { Tuple.Create("Session1", "Metric1"), Tuple.Create("Session2", "Metric2") });
            Assert.IsTrue(expectedIds.SetEquals(collectionConfiguration.TelemetryMetadata.Single().Item1));
        }

        [TestMethod]
        public void CollectionConfigurationReportsInvalidFilterTest()
        {
            // ARRANGE
            string[] errors;
            var filterInfo = new FilterInfo() { FieldName = "NonExistentFieldName", Predicate = Predicate.Equal, Comparand = "Request" };
            var metrics = new[]
                              {
                                  new OperationalizedMetricInfo()
                                      {
                                          SessionId = "Session1",
                                          Id = "Metric1",
                                          TelemetryType = TelemetryType.Request,
                                          Projection = "Name",
                                          Aggregation = AggregationType.Avg,
                                          Filters = new[] { filterInfo }
                                      }
                              };

            // ACT
            var collectionConfiguration = new CollectionConfiguration(new CollectionConfigurationInfo() { Metrics = metrics }, out errors);

            // ASSERT
            Assert.AreEqual(1, collectionConfiguration.RequestMetrics.Count());
            Assert.AreEqual(1, collectionConfiguration.TelemetryMetadata.Count());
            Assert.IsTrue(errors.Single().Contains("NonExistentFieldName"));
        }

        [TestMethod]
        public void CollectionConfigurationReportsInvalidMetricTest()
        {
            // ARRANGE
            string[] errors;
            var filterInfo = new FilterInfo() { FieldName = "Name", Predicate = Predicate.Equal, Comparand = "Request" };
            var metrics = new[]
                              {
                                  new OperationalizedMetricInfo()
                                      {
                                          SessionId = "Session1",
                                          Id = "Metric1",
                                          TelemetryType = TelemetryType.Request,
                                          Projection = "NonExistentFieldName",
                                          Aggregation = AggregationType.Avg,
                                          Filters = new[] { filterInfo }
                                      }
                              };

            // ACT
            var collectionConfiguration = new CollectionConfiguration(new CollectionConfigurationInfo() { Metrics = metrics }, out errors);

            // ASSERT
            Assert.AreEqual(0, collectionConfiguration.RequestMetrics.Count());
            Assert.AreEqual(0, collectionConfiguration.TelemetryMetadata.Count());
            Assert.IsTrue(errors.Single().Contains("NonExistentFieldName"));
        }

        [TestMethod]
        public void CollectionConfigurationReportsMultipleInvalidFiltersAndMetricsTest()
        {
            // ARRANGE
            string[] errors;
            var filterInfo1 = new FilterInfo() { FieldName = "NonExistentFilterFieldName1", Predicate = Predicate.Equal, Comparand = "Request" };
            var filterInfo2 = new FilterInfo() { FieldName = "NonExistentFilterFieldName2", Predicate = Predicate.Equal, Comparand = "Request" };
            var metrics = new[]
                              {
                                  new OperationalizedMetricInfo()
                                      {
                                          SessionId = "Session1",
                                          Id = "Metric1",
                                          TelemetryType = TelemetryType.Request,
                                          Projection = "NonExistentProjectionName1",
                                          Aggregation = AggregationType.Avg,
                                          Filters = new[] { filterInfo1, filterInfo2 }
                                      },
                                  new OperationalizedMetricInfo()
                                      {
                                          SessionId = "Session2",
                                          Id = "Metric2",
                                          TelemetryType = TelemetryType.Request,
                                          Projection = "NonExistentProjectionName2",
                                          Aggregation = AggregationType.Avg,
                                          Filters = new[] { filterInfo1, filterInfo2 }
                                      }
                              };

            // ACT
            var collectionConfiguration = new CollectionConfiguration(new CollectionConfigurationInfo() { Metrics = metrics }, out errors);

            // ASSERT
            Assert.AreEqual(0, collectionConfiguration.RequestMetrics.Count());
            Assert.AreEqual(0, collectionConfiguration.TelemetryMetadata.Count());

            Assert.AreEqual(6, errors.Length);
            Assert.IsTrue(errors[0].Contains("NonExistentFilterFieldName1"));
            Assert.IsTrue(errors[1].Contains("NonExistentFilterFieldName2"));
            Assert.IsTrue(errors[2].Contains("NonExistentProjectionName1"));
            Assert.IsTrue(errors[3].Contains("NonExistentFilterFieldName1"));
            Assert.IsTrue(errors[4].Contains("NonExistentFilterFieldName2"));
            Assert.IsTrue(errors[5].Contains("NonExistentProjectionName2"));
        }
    }
}