namespace Unit.Tests
{
    using System;
    using System.Linq;

    using Microsoft.ApplicationInsights.Extensibility.Filtering;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class CollectionConfigurationTests
    {
        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void CollectionConfigurationThrowsOnNullInput()
        {
            // ARRANGE

            // ACT
            string[] errors;
            new CollectionConfiguration(null, out errors);

            // ASSERT
        }

        [TestMethod]
        public void CollectionConfigurationCreatesMetrics()
        {
            // ARRANGE
            string[] errors;
            var filters = new[]
            {
                new FilterConjunctionGroupInfo()
                {
                    Filters = new[] { new FilterInfo() { FieldName = "Name", Predicate = Predicate.Equal, Comparand = "Request1" } }
                }
            };

            var metrics = new[]
            {
                new OperationalizedMetricInfo()
                {
                    Id = "Metric0",
                    TelemetryType = TelemetryType.Request,
                    Projection = "Name",
                    Aggregation = AggregationType.Avg,
                    FilterGroups = filters
                },
                new OperationalizedMetricInfo()
                {
                    Id = "Metric1",
                    TelemetryType = TelemetryType.Request,
                    Projection = "Id",
                    Aggregation = AggregationType.Sum,
                    FilterGroups = filters
                },
                new OperationalizedMetricInfo()
                {
                    Id = "Metric2",
                    TelemetryType = TelemetryType.Dependency,
                    Projection = "Name",
                    Aggregation = AggregationType.Avg,
                    FilterGroups = filters
                },
                new OperationalizedMetricInfo()
                {
                    Id = "Metric3",
                    TelemetryType = TelemetryType.Exception,
                    Projection = "Message",
                    Aggregation = AggregationType.Avg,
                    FilterGroups = filters
                },
                new OperationalizedMetricInfo()
                {
                    Id = "Metric4",
                    TelemetryType = TelemetryType.Event,
                    Projection = "Name",
                    Aggregation = AggregationType.Avg,
                    FilterGroups = filters
                },
                new OperationalizedMetricInfo()
                {
                    Id = "Metric5",
                    TelemetryType = TelemetryType.Metric,
                    Projection = "Value",
                    Aggregation = AggregationType.Avg,
                    FilterGroups = filters
                }
            };

            // ACT
            var collectionConfiguration = new CollectionConfiguration(new CollectionConfigurationInfo() { Metrics = metrics }, out errors);

            // ASSERT
            Assert.AreEqual("Metric0", collectionConfiguration.RequestMetrics.First().Id);
            Assert.AreEqual("Metric1", collectionConfiguration.RequestMetrics.Last().Id);
            Assert.AreEqual("Metric2", collectionConfiguration.DependencyMetrics.Single().Id);
            Assert.AreEqual("Metric3", collectionConfiguration.ExceptionMetrics.Single().Id);
            Assert.AreEqual("Metric4", collectionConfiguration.EventMetrics.Single().Id);
            Assert.AreEqual("Metric5", collectionConfiguration.MetricMetrics.Single().Id);

            Assert.AreEqual(5, collectionConfiguration.TelemetryMetadata.Count());
            Assert.AreEqual("Metric5", collectionConfiguration.MetricMetrics.Single().Id);
        }

        [TestMethod]
        public void CollectionConfigurationReportsMetricsWithDuplicateIds()
        {
            // ARRANGE
            string[] errors;
            var filter1 = new FilterInfo() { FieldName = "Name", Predicate = Predicate.Equal, Comparand = "Request1" };
            var filter2 = new FilterInfo() { FieldName = "Name", Predicate = Predicate.Equal, Comparand = "Request1" };
            var metrics = new[]
            {
                new OperationalizedMetricInfo()
                {
                    Id = "Metric1",
                    TelemetryType = TelemetryType.Request,
                    Projection = "Name",
                    Aggregation = AggregationType.Avg,
                    FilterGroups = new[] { new FilterConjunctionGroupInfo() { Filters = new[] { filter1 } } }
                },
                new OperationalizedMetricInfo()
                {
                    Id = "Metric1",
                    TelemetryType = TelemetryType.Request,
                    Projection = "Name",
                    Aggregation = AggregationType.Avg,
                    FilterGroups = new[] { new FilterConjunctionGroupInfo() { Filters = new[] { filter2 } } }
                },
                new OperationalizedMetricInfo()
                {
                    Id = "Metric2",
                    TelemetryType = TelemetryType.Request,
                    Projection = "Name",
                    Aggregation = AggregationType.Avg,
                    FilterGroups = new[] { new FilterConjunctionGroupInfo() { Filters = new[] { filter2 } } }
                }
            };

            // ACT
            var collectionConfiguration = new CollectionConfiguration(new CollectionConfigurationInfo() { Metrics = metrics }, out errors);

            // ASSERT
            Assert.AreEqual(2, collectionConfiguration.RequestMetrics.Count());
            Assert.AreEqual("Metric1", collectionConfiguration.TelemetryMetadata.First().Item1);
            Assert.AreEqual("Metric2", collectionConfiguration.TelemetryMetadata.Last().Item1);
            Assert.IsTrue(errors.Single().Contains("Metric1"));
        }

        [TestMethod]
        public void CollectionConfigurationReportsInvalidFilterForMetric()
        {
            // ARRANGE
            string[] errors;
            var filterInfo = new FilterInfo() { FieldName = "NonExistentFieldName", Predicate = Predicate.Equal, Comparand = "Request" };
            var metrics = new[]
            {
                new OperationalizedMetricInfo()
                {
                    Id = "Metric1",
                    TelemetryType = TelemetryType.Request,
                    Projection = "Name",
                    Aggregation = AggregationType.Avg,
                    FilterGroups = new[] { new FilterConjunctionGroupInfo() { Filters = new[] { filterInfo } } }
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
        public void CollectionConfigurationReportsInvalidMetric()
        {
            // ARRANGE
            string[] errors;
            var filterInfo = new FilterInfo() { FieldName = "Name", Predicate = Predicate.Equal, Comparand = "Request" };
            var metrics = new[]
            {
                new OperationalizedMetricInfo()
                {
                    Id = "Metric1",
                    TelemetryType = TelemetryType.Request,
                    Projection = "NonExistentFieldName",
                    Aggregation = AggregationType.Avg,
                    FilterGroups = new[] { new FilterConjunctionGroupInfo() { Filters = new[] { filterInfo } } }
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
        public void CollectionConfigurationReportsMultipleInvalidFiltersAndMetrics()
        {
            // ARRANGE
            string[] errors;
            var filterInfo1 = new FilterInfo() { FieldName = "NonExistentFilterFieldName1", Predicate = Predicate.Equal, Comparand = "Request" };
            var filterInfo2 = new FilterInfo() { FieldName = "NonExistentFilterFieldName2", Predicate = Predicate.Equal, Comparand = "Request" };
            var metrics = new[]
            {
                new OperationalizedMetricInfo()
                {
                    Id = "Metric1",
                    TelemetryType = TelemetryType.Request,
                    Projection = "NonExistentProjectionName1",
                    Aggregation = AggregationType.Avg,
                    FilterGroups = new[] { new FilterConjunctionGroupInfo() { Filters = new[] { filterInfo1, filterInfo2 } } }
                },
                new OperationalizedMetricInfo()
                {
                    Id = "Metric2",
                    TelemetryType = TelemetryType.Request,
                    Projection = "NonExistentProjectionName2",
                    Aggregation = AggregationType.Avg,
                    FilterGroups = new[] { new FilterConjunctionGroupInfo() { Filters = new[] { filterInfo1, filterInfo2 } } }
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

        [TestMethod]
        public void CollectionConfigurationCreatesDocumentStreams()
        {
            // ARRANGE
            string[] errors;
            var documentStreamInfos = new[]
            {
                new DocumentStreamInfo()
                {
                    Id = "Stream1",
                    DocumentFilterGroups =
                        new[]
                        {
                            new DocumentFilterConjunctionGroupInfo()
                            {
                                TelemetryType = TelemetryType.Request,
                                Filters = new FilterConjunctionGroupInfo() { Filters = new FilterInfo[0] }
                            }
                        }
                },
                new DocumentStreamInfo()
                {
                    Id = "Stream2",
                    DocumentFilterGroups =
                        new[]
                        {
                            new DocumentFilterConjunctionGroupInfo()
                            {
                                TelemetryType = TelemetryType.Dependency,
                                Filters = new FilterConjunctionGroupInfo() { Filters = new FilterInfo[0] }
                            }
                        }
                }
            };

            var collectionConfigurationInfo = new CollectionConfigurationInfo() { DocumentStreams = documentStreamInfos, ETag = "ETag1" };

            // ACT
            var collectionConfiguration = new CollectionConfiguration(collectionConfigurationInfo, out errors);

            // ASSERT
            Assert.AreEqual(2, collectionConfiguration.DocumentStreams.Count());
            Assert.AreEqual("Stream1", collectionConfiguration.DocumentStreams.First().Id);
            Assert.AreEqual("Stream2", collectionConfiguration.DocumentStreams.Last().Id);
        }

        [TestMethod]
        public void CollectionConfigurationReportsDocumentStreamsWithDuplicateIds()
        {
            // ARRANGE
            string[] errors;
            var documentStreamInfos = new[]
            {
                new DocumentStreamInfo()
                {
                    Id = "Stream1",
                    DocumentFilterGroups =
                        new[]
                        {
                            new DocumentFilterConjunctionGroupInfo()
                            {
                                TelemetryType = TelemetryType.Request,
                                Filters = new FilterConjunctionGroupInfo() { Filters = new FilterInfo[0] }
                            }
                        }
                },
                new DocumentStreamInfo()
                {
                    Id = "Stream1",
                    DocumentFilterGroups =
                        new[]
                        {
                            new DocumentFilterConjunctionGroupInfo()
                            {
                                TelemetryType = TelemetryType.Dependency,
                                Filters = new FilterConjunctionGroupInfo() { Filters = new FilterInfo[0] }
                            }
                        }
                },
                new DocumentStreamInfo()
                {
                    Id = "Stream2",
                    DocumentFilterGroups =
                        new[]
                        {
                            new DocumentFilterConjunctionGroupInfo()
                            {
                                TelemetryType = TelemetryType.Dependency,
                                Filters = new FilterConjunctionGroupInfo() { Filters = new FilterInfo[0] }
                            }
                        }
                }
            };

            var collectionConfigurationInfo = new CollectionConfigurationInfo() { DocumentStreams = documentStreamInfos, ETag = "ETag1" };

            // ACT
            var collectionConfiguration = new CollectionConfiguration(collectionConfigurationInfo, out errors);

            // ASSERT
            Assert.AreEqual(2, collectionConfiguration.DocumentStreams.Count());
            Assert.AreEqual("Stream1", collectionConfiguration.DocumentStreams.First().Id);
            Assert.AreEqual("Stream2", collectionConfiguration.DocumentStreams.Last().Id);
            Assert.IsTrue(errors.Single().Contains("Stream1"));
        }

        [TestMethod]
        public void CollectionConfigurationReportsInvalidFilterForDocumentStreams()
        {
            // ARRANGE
            string[] errors;
            var documentStreamInfos = new[]
            {
                new DocumentStreamInfo()
                {
                    Id = "Stream1",
                    DocumentFilterGroups =
                        new[]
                        {
                            new DocumentFilterConjunctionGroupInfo()
                            {
                                TelemetryType = TelemetryType.Request,
                                Filters = new FilterConjunctionGroupInfo() { Filters = new[] { new FilterInfo() { FieldName = "NonExistentFieldName" } } }
                            }
                        }
                }
            };

            var collectionConfigurationInfo = new CollectionConfigurationInfo() { DocumentStreams = documentStreamInfos, ETag = "ETag1" };

            // ACT
            var collectionConfiguration = new CollectionConfiguration(collectionConfigurationInfo, out errors);

            // ASSERT
            Assert.AreEqual(1, collectionConfiguration.DocumentStreams.Count());
            Assert.IsTrue(errors.Single().Contains("NonExistentFieldName"));
        }

        [TestMethod]
        public void CollectionConfigurationReportsInvalidFilterGroupForDocumentStreams()
        {
            // ARRANGE
            string[] errors;
            var documentStreamInfos = new[]
            {
                new DocumentStreamInfo()
                {
                    Id = "Stream1",
                    DocumentFilterGroups =
                        new[]
                        {
                            new DocumentFilterConjunctionGroupInfo()
                            {
                                TelemetryType = (TelemetryType)505,
                                Filters = new FilterConjunctionGroupInfo() { Filters = new FilterInfo[0] }
                            }
                        }
                }
            };

            var collectionConfigurationInfo = new CollectionConfigurationInfo() { DocumentStreams = documentStreamInfos, ETag = "ETag1" };

            // ACT
            var collectionConfiguration = new CollectionConfiguration(collectionConfigurationInfo, out errors);

            // ASSERT
            Assert.AreEqual(1, collectionConfiguration.DocumentStreams.Count());
            Assert.IsTrue(errors.Single().Contains("505"));
        }

        [TestMethod]
        public void CollectionConfigurationReportsMultipleInvalidFiltersAndDocumentStreams()
        {
            // ARRANGE
            string[] errors;
            var documentStreamInfos = new[]
            {
                new DocumentStreamInfo()
                {
                    Id = "Stream1",
                    DocumentFilterGroups =
                        new[]
                        {
                            new DocumentFilterConjunctionGroupInfo()
                            {
                                TelemetryType = (TelemetryType)505,
                                Filters =
                                    new FilterConjunctionGroupInfo()
                                    {
                                        Filters =
                                            new[]
                                            {
                                                new FilterInfo() { FieldName = "NonExistentFilterFieldName1" },
                                                new FilterInfo() { FieldName = "NonExistentFilterFieldName2" }
                                            }
                                    }
                            }
                        }
                },
                new DocumentStreamInfo()
                {
                    Id = "Stream2",
                    DocumentFilterGroups =
                        new[]
                        {
                            new DocumentFilterConjunctionGroupInfo()
                            {
                                TelemetryType = TelemetryType.Request,
                                Filters =
                                    new FilterConjunctionGroupInfo()
                                    {
                                        Filters =
                                            new[]
                                            {
                                                new FilterInfo() { FieldName = "NonExistentFilterFieldName3" },
                                                new FilterInfo() { FieldName = "NonExistentFilterFieldName4" }
                                            }
                                    }
                            }
                        }
                }
            };

            var collectionConfigurationInfo = new CollectionConfigurationInfo() { DocumentStreams = documentStreamInfos, ETag = "ETag1" };

            // ACT
            var collectionConfiguration = new CollectionConfiguration(collectionConfigurationInfo, out errors);

            // ASSERT
            Assert.AreEqual(2, collectionConfiguration.DocumentStreams.Count());
            Assert.AreEqual("Stream1", collectionConfiguration.DocumentStreams.First().Id);
            Assert.AreEqual("Stream2", collectionConfiguration.DocumentStreams.Last().Id);

            Assert.AreEqual(3, errors.Length);
            Assert.IsTrue(errors[0].Contains("505"));
            Assert.IsTrue(errors[1].Contains("NonExistentFilterFieldName3"));
            Assert.IsTrue(errors[2].Contains("NonExistentFilterFieldName4"));
        }
    }
}