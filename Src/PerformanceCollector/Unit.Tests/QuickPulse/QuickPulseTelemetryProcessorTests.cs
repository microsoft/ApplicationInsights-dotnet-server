namespace Unit.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;

    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.Filtering;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;
    using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.Implementation.QuickPulse;
    using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.QuickPulse;
    using Microsoft.ApplicationInsights.Web.Helpers;
    using Microsoft.ManagementServices.RealTimeDataProcessing.QuickPulseService;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class QuickPulseTelemetryProcessorTests
    {
        private const int MaxFieldLength = 32768;

        private static readonly CollectionConfiguration EmptyCollectionConfiguration =
            new CollectionConfiguration(
                new CollectionConfigurationInfo() { ETag = string.Empty, Metrics = new OperationalizedMetricInfo[0] },
                out errors, new ClockMock());

        private static CollectionConfigurationError[] errors;

        [TestInitialize]
        public void TestInitialize()
        {
            QuickPulseTestHelper.ClearEnvironment();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void QuickPulseTelemetryProcessorThrowsIfNextIsNull()
        {
            new QuickPulseTelemetryProcessor(null);
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorRegistersWithModule()
        {
            // ARRANGE
            var module = new QuickPulseTelemetryModule(null, null, null, null, null, null, null);

            TelemetryModules.Instance.Modules.Add(module);

            // ACT
            var spy = new SimpleTelemetryProcessorSpy();
            var telemetryProcessor = new QuickPulseTelemetryProcessor(spy);
            telemetryProcessor.Initialize(new TelemetryConfiguration());

            // ASSERT
            Assert.AreEqual(telemetryProcessor, QuickPulseTestHelper.GetTelemetryProcessors(module).Single());
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorCallsNext()
        {
            // ARRANGE
            var spy = new SimpleTelemetryProcessorSpy();
            var telemetryProcessor = new QuickPulseTelemetryProcessor(spy);

            // ACT
            telemetryProcessor.Process(new RequestTelemetry() { Context = { InstrumentationKey = "some ikey" } });

            // ASSERT
            Assert.AreEqual(1, spy.ReceivedCalls);
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorKeepsAccurateCountOfRequests()
        {
            // ARRANGE
            var accumulatorManager = new QuickPulseDataAccumulatorManager(EmptyCollectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = "some ikey" });

            // ACT
            telemetryProcessor.Process(
                new RequestTelemetry()
                {
                    Success = false,
                    ResponseCode = "200",
                    Duration = TimeSpan.FromSeconds(1),
                    Context = { InstrumentationKey = "some ikey" }
                });
            telemetryProcessor.Process(
                new RequestTelemetry()
                {
                    Success = true,
                    ResponseCode = "200",
                    Duration = TimeSpan.FromSeconds(2),
                    Context = { InstrumentationKey = "some ikey" }
                });
            telemetryProcessor.Process(
                new RequestTelemetry()
                {
                    Success = false,
                    ResponseCode = string.Empty,
                    Duration = TimeSpan.FromSeconds(3),
                    Context = { InstrumentationKey = "some ikey" }
                });
            telemetryProcessor.Process(
                new RequestTelemetry()
                {
                    Success = null,
                    ResponseCode = string.Empty,
                    Duration = TimeSpan.FromSeconds(4),
                    Context = { InstrumentationKey = "some ikey" }
                });
            telemetryProcessor.Process(
                new RequestTelemetry()
                {
                    Success = true,
                    ResponseCode = string.Empty,
                    Duration = TimeSpan.FromSeconds(5),
                    Context = { InstrumentationKey = "some ikey" }
                });
            telemetryProcessor.Process(
                new RequestTelemetry()
                {
                    Success = null,
                    ResponseCode = "404",
                    Duration = TimeSpan.FromSeconds(6),
                    Context = { InstrumentationKey = "some ikey" }
                });

            // ASSERT
            Assert.AreEqual(6, accumulatorManager.CurrentDataAccumulator.AIRequestCount);
            Assert.AreEqual(
                1 + 2 + 3 + 4 + 5 + 6,
                TimeSpan.FromTicks(accumulatorManager.CurrentDataAccumulator.AIRequestDurationInTicks).TotalSeconds);
            Assert.AreEqual(4, accumulatorManager.CurrentDataAccumulator.AIRequestSuccessCount);
            Assert.AreEqual(2, accumulatorManager.CurrentDataAccumulator.AIRequestFailureCount);
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorKeepsAccurateCountOfDependencies()
        {
            // ARRANGE
            var accumulatorManager = new QuickPulseDataAccumulatorManager(EmptyCollectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = "some ikey" });

            // ACT
            telemetryProcessor.Process(
                new DependencyTelemetry() { Success = true, Duration = TimeSpan.FromSeconds(1), Context = { InstrumentationKey = "some ikey" } });
            telemetryProcessor.Process(
                new DependencyTelemetry() { Success = true, Duration = TimeSpan.FromSeconds(1), Context = { InstrumentationKey = "some ikey" } });
            telemetryProcessor.Process(
                new DependencyTelemetry() { Success = false, Duration = TimeSpan.FromSeconds(2), Context = { InstrumentationKey = "some ikey" } });
            telemetryProcessor.Process(
                new DependencyTelemetry() { Success = null, Duration = TimeSpan.FromSeconds(3), Context = { InstrumentationKey = "some ikey" } });

            // ASSERT
            Assert.AreEqual(4, accumulatorManager.CurrentDataAccumulator.AIDependencyCallCount);
            Assert.AreEqual(1 + 1 + 2 + 3, TimeSpan.FromTicks(accumulatorManager.CurrentDataAccumulator.AIDependencyCallDurationInTicks).TotalSeconds);
            Assert.AreEqual(2, accumulatorManager.CurrentDataAccumulator.AIDependencyCallSuccessCount);
            Assert.AreEqual(1, accumulatorManager.CurrentDataAccumulator.AIDependencyCallFailureCount);
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorKeepsAccurateCountOfExceptions()
        {
            // ARRANGE
            var accumulatorManager = new QuickPulseDataAccumulatorManager(EmptyCollectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = "some ikey" });

            // ACT
            telemetryProcessor.Process(new ExceptionTelemetry() { Context = { InstrumentationKey = "some ikey" } });
            telemetryProcessor.Process(new ExceptionTelemetry() { Context = { InstrumentationKey = "some ikey" } });
            telemetryProcessor.Process(new ExceptionTelemetry() { Context = { InstrumentationKey = "some ikey" } });

            // ASSERT
            Assert.AreEqual(3, accumulatorManager.CurrentDataAccumulator.AIExceptionCount);
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorStopsCollection()
        {
            // ARRANGE
            var accumulatorManager = new QuickPulseDataAccumulatorManager(EmptyCollectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            var endpoint = new Uri("http://microsoft.com");
            var config = new TelemetryConfiguration() { InstrumentationKey = "some ikey" };

            // ACT
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(accumulatorManager, endpoint, config);
            telemetryProcessor.Process(new RequestTelemetry() { Context = { InstrumentationKey = "some ikey" } });
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StopCollection();
            telemetryProcessor.Process(new DependencyTelemetry() { Context = { InstrumentationKey = "some ikey" } });

            // ASSERT
            Assert.AreEqual(1, accumulatorManager.CurrentDataAccumulator.AIRequestCount);
            Assert.AreEqual(0, accumulatorManager.CurrentDataAccumulator.AIDependencyCallCount);
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorIgnoresUnrelatedTelemetryItems()
        {
            // ARRANGE
            var accumulatorManager = new QuickPulseDataAccumulatorManager(EmptyCollectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = "some ikey" });

            // ACT
            telemetryProcessor.Process(new EventTelemetry() { Context = { InstrumentationKey = "some ikey" } });
            telemetryProcessor.Process(new ExceptionTelemetry() { Context = { InstrumentationKey = "some ikey" } });
            telemetryProcessor.Process(new MetricTelemetry() { Context = { InstrumentationKey = "some ikey" } });
            telemetryProcessor.Process(new PageViewTelemetry() { Context = { InstrumentationKey = "some ikey" } });
            telemetryProcessor.Process(new TraceTelemetry() { Context = { InstrumentationKey = "some ikey" } });

            // ASSERT
            Assert.AreEqual(0, accumulatorManager.CurrentDataAccumulator.AIRequestCount);
            Assert.AreEqual(0, accumulatorManager.CurrentDataAccumulator.AIDependencyCallCount);
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorIgnoresTelemetryItemsToDifferentInstrumentationKeys()
        {
            // ARRANGE
            var accumulatorManager = new QuickPulseDataAccumulatorManager(EmptyCollectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());

            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = "some ikey" });

            // ACT
            telemetryProcessor.Process(new RequestTelemetry() { Context = { InstrumentationKey = "some other ikey" } });
            telemetryProcessor.Process(new RequestTelemetry() { Context = { InstrumentationKey = "some ikey" } });

            // ASSERT
            Assert.AreEqual(1, accumulatorManager.CurrentDataAccumulator.AIRequestCount);
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorHandlesMultipleThreadsCorrectly()
        {
            // ARRANGE
            var accumulatorManager = new QuickPulseDataAccumulatorManager(EmptyCollectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());

            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = "some ikey" });

            // expected data loss if threading is misimplemented is around 10% (established through experiment)
            int taskCount = 10000;
            var tasks = new List<Task>(taskCount);

            for (int i = 0; i < taskCount; i++)
            {
                var requestTelemetry = new RequestTelemetry()
                {
                    ResponseCode = (i % 2 == 0) ? "200" : "500",
                    Duration = TimeSpan.FromMilliseconds(i),
                    Context = { InstrumentationKey = "some ikey" }
                };

                var task = new Task(() => telemetryProcessor.Process(requestTelemetry));
                tasks.Add(task);
            }

            // ACT
            tasks.ForEach(task => task.Start());

            Task.WaitAll(tasks.ToArray());

            // ASSERT
            Assert.AreEqual(taskCount, accumulatorManager.CurrentDataAccumulator.AIRequestCount);
            Assert.AreEqual(taskCount / 2, accumulatorManager.CurrentDataAccumulator.AIRequestSuccessCount);
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorSwitchesBetweenMultipleAccumulatorManagers()
        {
            // ARRANGE
            var accumulatorManager1 = new QuickPulseDataAccumulatorManager(EmptyCollectionConfiguration);
            var accumulatorManager2 = new QuickPulseDataAccumulatorManager(EmptyCollectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());

            // ACT
            var serviceEndpoint = new Uri("http://microsoft.com");
            var config = new TelemetryConfiguration() { InstrumentationKey = "some ikey" };
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(accumulatorManager1, serviceEndpoint, config);
            telemetryProcessor.Process(new RequestTelemetry() { Context = { InstrumentationKey = "some ikey" } });
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StopCollection();

            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(accumulatorManager2, serviceEndpoint, config);
            telemetryProcessor.Process(new DependencyTelemetry() { Context = { InstrumentationKey = "some ikey" } });
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StopCollection();

            // ASSERT
            Assert.AreEqual(1, accumulatorManager1.CurrentDataAccumulator.AIRequestCount);
            Assert.AreEqual(0, accumulatorManager1.CurrentDataAccumulator.AIDependencyCallCount);

            Assert.AreEqual(0, accumulatorManager2.CurrentDataAccumulator.AIRequestCount);
            Assert.AreEqual(1, accumulatorManager2.CurrentDataAccumulator.AIDependencyCallCount);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void QuickPulseTelemetryProcessorMustBeStoppedBeforeReceivingStartCommand()
        {
            // ARRANGE
            var accumulatorManager = new QuickPulseDataAccumulatorManager(EmptyCollectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());

            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://test.com"),
                new TelemetryConfiguration());

            // ACT
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://test.com"),
                new TelemetryConfiguration());

            // ASSERT
            // must throw
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorFiltersOutDependencyCallsToQuickPulseServiceDuringCollection()
        {
            // ARRANGE
            var accumulatorManager = new QuickPulseDataAccumulatorManager(EmptyCollectionConfiguration);
            var simpleTelemetryProcessorSpy = new SimpleTelemetryProcessorSpy();
            var telemetryProcessor = new QuickPulseTelemetryProcessor(simpleTelemetryProcessorSpy);
            var config = new TelemetryConfiguration() { InstrumentationKey = "some ikey" };

            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("https://qps.cloudapp.net/endpoint.svc"),
                config);

            // ACT
            telemetryProcessor.Process(
                new DependencyTelemetry() { Name = "http://microsoft.ru", Context = { InstrumentationKey = config.InstrumentationKey } });
            telemetryProcessor.Process(
                new DependencyTelemetry() { Name = "http://qps.cloudapp.net/blabla", Context = { InstrumentationKey = config.InstrumentationKey } });
            telemetryProcessor.Process(
                new DependencyTelemetry() { Name = "https://bing.com", Context = { InstrumentationKey = config.InstrumentationKey } });

            // ASSERT
            Assert.AreEqual(2, simpleTelemetryProcessorSpy.ReceivedCalls);
            Assert.AreEqual("http://microsoft.ru", (simpleTelemetryProcessorSpy.ReceivedItems[0] as DependencyTelemetry).Name);
            Assert.AreEqual("https://bing.com", (simpleTelemetryProcessorSpy.ReceivedItems[1] as DependencyTelemetry).Name);
            Assert.AreEqual(2, accumulatorManager.CurrentDataAccumulator.AIDependencyCallCount);
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorFiltersOutDependencyCallsToDefaultQuickPulseServiceEndpointInIdleMode()
        {
            // ARRANGE
            var simpleTelemetryProcessorSpy = new SimpleTelemetryProcessorSpy();
            var telemetryProcessor = new QuickPulseTelemetryProcessor(simpleTelemetryProcessorSpy);

            // ACT
            telemetryProcessor.Process(new DependencyTelemetry() { Name = "http://microsoft.ru" });
            telemetryProcessor.Process(new DependencyTelemetry() { Name = "http://rt.services.visualstudio.com/blabla" });
            telemetryProcessor.Process(new DependencyTelemetry() { Name = "https://bing.com" });

            // ASSERT
            Assert.AreEqual(2, simpleTelemetryProcessorSpy.ReceivedCalls);
            Assert.AreEqual("http://microsoft.ru", (simpleTelemetryProcessorSpy.ReceivedItems[0] as DependencyTelemetry).Name);
            Assert.AreEqual("https://bing.com", (simpleTelemetryProcessorSpy.ReceivedItems[1] as DependencyTelemetry).Name);
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorCollectsFullTelemetryItemsAndDistributesThemAmongDocumentStreamsCorrectly()
        {
            // ARRANGE
            var requestsAndDependenciesDocumentStreamInfo = new DocumentStreamInfo()
            {
                Id = "StreamRequestsAndDependenciesAndExceptions",
                DocumentFilterGroups =
                    new[]
                    {
                        new DocumentFilterConjunctionGroupInfo()
                        {
                            TelemetryType = TelemetryType.Request,
                            Filters =
                                new FilterConjunctionGroupInfo
                                {
                                    Filters =
                                        new[]
                                        {
                                            new FilterInfo { FieldName = "ResponseCode", Predicate = Predicate.Equal, Comparand = "500" },
                                            new FilterInfo { FieldName = "Success", Predicate = Predicate.Equal, Comparand = "0" }
                                        }
                                }
                        },
                        new DocumentFilterConjunctionGroupInfo()
                        {
                            TelemetryType = TelemetryType.Dependency,
                            Filters =
                                new FilterConjunctionGroupInfo
                                {
                                    Filters = new[] { new FilterInfo { FieldName = "Duration", Predicate = Predicate.Equal, Comparand = "0.00:00:01" } }
                                }
                        },
                         new DocumentFilterConjunctionGroupInfo()
                        {
                            TelemetryType = TelemetryType.Exception,
                            Filters =
                                new FilterConjunctionGroupInfo
                                {
                                    Filters =
                                        new[]
                                        {
                                            new FilterInfo { FieldName = "CustomDimensions.Prop1", Predicate = Predicate.Equal, Comparand = "Val1" },
                                            new FilterInfo { FieldName = "CustomDimensions.Prop2", Predicate = Predicate.Equal, Comparand = "Val2" }
                                        }
                                }
                        },
                    }
            };

            var exceptionsEventsTracesDocumentStreamInfo = new DocumentStreamInfo()
            {
                Id = "StreamExceptionsEventsTraces",
                DocumentFilterGroups =
                    new[]
                    {
                        new DocumentFilterConjunctionGroupInfo()
                        {
                            TelemetryType = TelemetryType.Exception,
                            Filters =
                                new FilterConjunctionGroupInfo
                                {
                                    Filters =
                                        new[]
                                        {
                                            new FilterInfo { FieldName = "CustomDimensions.Prop1", Predicate = Predicate.Equal, Comparand = "Val1" },
                                            new FilterInfo { FieldName = "CustomDimensions.Prop2", Predicate = Predicate.Equal, Comparand = "Val2" }
                                        }
                                }
                        },
                        new DocumentFilterConjunctionGroupInfo()
                        {
                            TelemetryType = TelemetryType.Event,
                            Filters =
                                new FilterConjunctionGroupInfo
                                {
                                    Filters = new[] { new FilterInfo { FieldName = "Name", Predicate = Predicate.Equal, Comparand = "Event1" } }
                                }
                        },
                         new DocumentFilterConjunctionGroupInfo()
                        {
                            TelemetryType = TelemetryType.Trace,
                            Filters =
                                new FilterConjunctionGroupInfo
                                {
                                    Filters = new[] { new FilterInfo { FieldName = "Message", Predicate = Predicate.Equal, Comparand = "Trace1" } }
                                }
                        }
                    }
            };

            var collectionConfigurationInfo = new CollectionConfigurationInfo()
            {
                DocumentStreams = new[] { requestsAndDependenciesDocumentStreamInfo, exceptionsEventsTracesDocumentStreamInfo },
                ETag = "ETag1"
            };

            var collectionConfiguration = new CollectionConfiguration(collectionConfigurationInfo, out errors, new ClockMock());

            var accumulatorManager = new QuickPulseDataAccumulatorManager(collectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            var instrumentationKey = "some ikey";
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = instrumentationKey });

            // ACT
            var request = new RequestTelemetry()
            {
                Name = Guid.NewGuid().ToString(),
                Success = false,
                ResponseCode = "500",
                Duration = TimeSpan.FromSeconds(1),
                Properties = { { "Prop1", "Val1" }, { "Prop2", "Val2" }, { "Prop3", "Val3" }, { "Prop4", "Val4" } },
                Context = { InstrumentationKey = instrumentationKey }
            };

            var dependency = new DependencyTelemetry()
            {
                Name = Guid.NewGuid().ToString(),
                Success = false,
                Duration = TimeSpan.FromSeconds(1),
                Properties = { { "Prop1", "Val1" }, { "Prop2", "Val2" }, { "Prop3", "Val3" }, { "Prop4", "Val4" }, { "ErrorMessage", "EMValue" } },
                Context = { InstrumentationKey = instrumentationKey }
            };

            var exception = new ExceptionTelemetry(new ArgumentNullException())
            {
                Properties = { { "Prop1", "Val1" }, { "Prop2", "Val2" }, { "Prop3", "Val3" }, { "Prop4", "Val4" } },
                Context = { InstrumentationKey = instrumentationKey }
            };

            var @event = new EventTelemetry()
            {
                Name = "Event1",
                Properties = { { "Prop1", "Val1" }, { "Prop2", "Val2" }, { "Prop3", "Val3" }, { "Prop4", "Val4" } },
                Context = { InstrumentationKey = instrumentationKey }
            };

            var trace = new TraceTelemetry()
            {
                Message = "Trace1",
                Properties = { { "Prop1", "Val1" }, { "Prop2", "Val2" }, { "Prop3", "Val3" }, { "Prop4", "Val4" } },
                Context = { InstrumentationKey = instrumentationKey }
            };

            telemetryProcessor.Process(request);
            telemetryProcessor.Process(dependency);
            telemetryProcessor.Process(exception);
            telemetryProcessor.Process(@event);
            telemetryProcessor.Process(trace);

            // ASSERT
            var collectedTelemetry = accumulatorManager.CurrentDataAccumulator.TelemetryDocuments.ToArray().Reverse().ToArray();

            Assert.IsFalse(accumulatorManager.CurrentDataAccumulator.GlobalDocumentQuotaReached);

            Assert.AreEqual(5, accumulatorManager.CurrentDataAccumulator.TelemetryDocuments.Count);

            Assert.AreEqual(TelemetryDocumentType.Request, Enum.Parse(typeof(TelemetryDocumentType), collectedTelemetry[0].DocumentType));
            Assert.AreEqual(request.Name, ((RequestTelemetryDocument)collectedTelemetry[0]).Name);
            Assert.AreEqual(3, collectedTelemetry[0].Properties.Length);
            Assert.AreEqual("StreamRequestsAndDependenciesAndExceptions", collectedTelemetry[0].DocumentStreamIds.Single());
            Assert.IsTrue(collectedTelemetry[0].Properties.ToList().TrueForAll(pair => pair.Key.Contains("Prop") && pair.Value.Contains("Val")));

            Assert.AreEqual(TelemetryDocumentType.RemoteDependency, Enum.Parse(typeof(TelemetryDocumentType), collectedTelemetry[1].DocumentType));
            Assert.AreEqual(dependency.Name, ((DependencyTelemetryDocument)collectedTelemetry[1]).Name);
            Assert.AreEqual(3 + 1, collectedTelemetry[1].Properties.Length);
            Assert.AreEqual("StreamRequestsAndDependenciesAndExceptions", collectedTelemetry[1].DocumentStreamIds.Single());
            Assert.IsTrue(
                collectedTelemetry[1].Properties.ToList()
                    .TrueForAll(
                        pair => (pair.Key.Contains("Prop") && pair.Value.Contains("Val")) || (pair.Key == "ErrorMessage" && pair.Value == "EMValue")));

            Assert.AreEqual(TelemetryDocumentType.Exception, Enum.Parse(typeof(TelemetryDocumentType), collectedTelemetry[2].DocumentType));
            Assert.AreEqual(exception.Exception.ToString(), ((ExceptionTelemetryDocument)collectedTelemetry[2]).Exception);
            Assert.AreEqual(3, collectedTelemetry[2].Properties.Length);
            Assert.AreEqual(2, collectedTelemetry[2].DocumentStreamIds.Length);
            Assert.AreEqual("StreamRequestsAndDependenciesAndExceptions", collectedTelemetry[2].DocumentStreamIds.First());
            Assert.AreEqual("StreamExceptionsEventsTraces", collectedTelemetry[2].DocumentStreamIds.Last());
            Assert.IsTrue(collectedTelemetry[2].Properties.ToList().TrueForAll(pair => (pair.Key.Contains("Prop") && pair.Value.Contains("Val"))));

            Assert.AreEqual(TelemetryDocumentType.Event, Enum.Parse(typeof(TelemetryDocumentType), collectedTelemetry[3].DocumentType));
            Assert.AreEqual(@event.Name, ((EventTelemetryDocument)collectedTelemetry[3]).Name);
            Assert.AreEqual(3, collectedTelemetry[3].Properties.Length);
            Assert.AreEqual("StreamExceptionsEventsTraces", collectedTelemetry[3].DocumentStreamIds.Single());
            Assert.IsTrue(collectedTelemetry[3].Properties.ToList().TrueForAll(pair => (pair.Key.Contains("Prop") && pair.Value.Contains("Val"))));

            Assert.AreEqual(TelemetryDocumentType.Trace, Enum.Parse(typeof(TelemetryDocumentType), collectedTelemetry[4].DocumentType));
            Assert.AreEqual(trace.Message, ((TraceTelemetryDocument)collectedTelemetry[4]).Message);
            Assert.AreEqual(3, collectedTelemetry[4].Properties.Length);
            Assert.AreEqual("StreamExceptionsEventsTraces", collectedTelemetry[4].DocumentStreamIds.Single());
            Assert.IsTrue(collectedTelemetry[4].Properties.ToList().TrueForAll(pair => (pair.Key.Contains("Prop") && pair.Value.Contains("Val"))));
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorDoesNotCollectFullTelemetryItemsIfTypeIsNotMentionedInDocumentStream()
        {
            // ARRANGE
            var documentStreamInfo = new DocumentStreamInfo()
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
            };

            var collectionConfigurationInfo = new CollectionConfigurationInfo() { DocumentStreams = new[] { documentStreamInfo }, ETag = "ETag1" };

            var collectionConfiguration = new CollectionConfiguration(collectionConfigurationInfo, out errors, new ClockMock());

            var accumulatorManager = new QuickPulseDataAccumulatorManager(collectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            var instrumentationKey = "some ikey";
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = instrumentationKey });

            // ACT
            var request = new RequestTelemetry()
            {
                Context = { InstrumentationKey = instrumentationKey }
            };

            var dependency = new DependencyTelemetry()
            {
                Context = { InstrumentationKey = instrumentationKey }
            };

            telemetryProcessor.Process(request);
            telemetryProcessor.Process(dependency);
            
            // ASSERT
            Assert.AreEqual(TelemetryDocumentType.RemoteDependency.ToString(), accumulatorManager.CurrentDataAccumulator.TelemetryDocuments.ToArray().Reverse().ToArray().Single().DocumentType);
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorDoesNotCollectFullRequestTelemetryItemsOnceQuotaIsExhaustedIndependentlyPerDocumentStream()
        {
            // ARRANGE
            var documentStreamInfos = new[]
            {
                new DocumentStreamInfo()
                {
                    Id = "StreamAll",
                    DocumentFilterGroups =
                        new[]
                        {
                            new DocumentFilterConjunctionGroupInfo()
                            {
                                TelemetryType = TelemetryType.Request,
                                Filters = new FilterConjunctionGroupInfo { Filters = new FilterInfo[0] }
                            }
                        }
                },
                new DocumentStreamInfo()
                {
                    Id = "StreamSuccessOnly",
                    DocumentFilterGroups =
                        new[]
                        {
                            new DocumentFilterConjunctionGroupInfo()
                            {
                                TelemetryType = TelemetryType.Request,
                                Filters =
                                    new FilterConjunctionGroupInfo
                                    {
                                        Filters = new[] { new FilterInfo() { FieldName = "Success", Predicate = Predicate.Equal, Comparand = "true" } }
                                    }
                            }
                        }
                }
            };

            var collectionConfigurationInfo = new CollectionConfigurationInfo() { ETag = "ETag1", DocumentStreams = documentStreamInfos };


            var timeProvider = new ClockMock();
            var collectionConfiguration = new CollectionConfiguration(collectionConfigurationInfo, out errors, timeProvider);
            var accumulatorManager = new QuickPulseDataAccumulatorManager(collectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            var instrumentationKey = "some ikey";
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = instrumentationKey });

            // ACT
            int counter = 0;
            for (int i = 0; i < 100; i++)
            {
                var request = new RequestTelemetry()
                {
                    Success = i == 0,
                    Duration = TimeSpan.FromSeconds(counter++),
                    Context = { InstrumentationKey = instrumentationKey }
                };

                telemetryProcessor.Process(request);
            }

            timeProvider.FastForward(TimeSpan.FromSeconds(30));

            for (int i = 0; i < 100; i++)
            {
                var request = new RequestTelemetry()
                {
                    Success = i < 20,
                    Duration = TimeSpan.FromSeconds(counter++),
                    Context = { InstrumentationKey = instrumentationKey }
                };

                telemetryProcessor.Process(request);
            }

            // ASSERT
            Assert.AreEqual(0, errors.Length);

            var collectedTelemetryStreamAll =
                accumulatorManager.CurrentDataAccumulator.TelemetryDocuments.Where(document => document.DocumentStreamIds.Contains("StreamAll"))
                    .ToArray()
                    .Reverse()
                    .Cast<RequestTelemetryDocument>()
                    .ToArray();

            var collectedTelemetryStreamSuccessOnly =
                accumulatorManager.CurrentDataAccumulator.TelemetryDocuments.Where(
                    document => document.DocumentStreamIds.Contains("StreamSuccessOnly"))
                    .ToArray()
                    .Reverse()
                    .Cast<RequestTelemetryDocument>()
                    .ToArray();


            // the quota is 3 initially, then 0.5 every second (but not more than 30)

            // StreamAll has collected the initial quota of the first 100, then the additional accrued quota from the second 100
            Assert.AreEqual(3 + 15, collectedTelemetryStreamAll.Length);

            // out of the first 100 items we expect to see the initial quota of 3
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(i, collectedTelemetryStreamAll[i].Duration.TotalSeconds);
            }

            // out of the second 100 items we expect to see items 100 through 114 (the new quota for 30 seconds is 15)
            for (int i = 0; i < 15; i++)
            {
                Assert.AreEqual(100 + i, collectedTelemetryStreamAll[3 + i].Duration.TotalSeconds);
            }

            // StreamSuccessOnly never hit the quota during the first 100. It got 1 and had 2 quota left at the end of it. 
            // Out of the second 100, it got 2 that were left over in the quota + the newly accrued quota of 15
            Assert.AreEqual(1 + 17, collectedTelemetryStreamSuccessOnly.Length);

            // just one item of the first 100
            Assert.AreEqual(0, collectedTelemetryStreamSuccessOnly[0].Duration.TotalSeconds);

            // 17 (15 accrued quota + 2 left over quota) from the second 100
            for (int i = 0; i < 17; i ++)
            {
                Assert.AreEqual(100 + i, collectedTelemetryStreamSuccessOnly[1 + i].Duration.TotalSeconds);
            }
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorDoesNotCollectFullDependencyTelemetryItemsOnceQuotaIsExhaustedIndependentlyPerDocumentStream()
        {
            // ARRANGE
            var documentStreamInfos = new[]
            {
                new DocumentStreamInfo()
                {
                    Id = "StreamAll",
                    DocumentFilterGroups =
                        new[]
                        {
                            new DocumentFilterConjunctionGroupInfo()
                            {
                                TelemetryType = TelemetryType.Dependency,
                                Filters = new FilterConjunctionGroupInfo { Filters = new FilterInfo[0] }
                            }
                        }
                },
                new DocumentStreamInfo()
                {
                    Id = "StreamSuccessOnly",
                    DocumentFilterGroups =
                        new[]
                        {
                            new DocumentFilterConjunctionGroupInfo()
                            {
                                TelemetryType = TelemetryType.Dependency,
                                Filters =
                                    new FilterConjunctionGroupInfo
                                    {
                                        Filters = new[] { new FilterInfo() { FieldName = "Success", Predicate = Predicate.Equal, Comparand = "true" } }
                                    }
                            }
                        }
                }
            };

            var collectionConfigurationInfo = new CollectionConfigurationInfo() { ETag = "ETag1", DocumentStreams = documentStreamInfos };


            var timeProvider = new ClockMock();
            var collectionConfiguration = new CollectionConfiguration(collectionConfigurationInfo, out errors, timeProvider);
            var accumulatorManager = new QuickPulseDataAccumulatorManager(collectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            var instrumentationKey = "some ikey";
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = instrumentationKey });

            // ACT
            int counter = 0;
            for (int i = 0; i < 100; i++)
            {
                var request = new DependencyTelemetry()
                {
                    Success = i == 0,
                    Duration = TimeSpan.FromSeconds(counter++),
                    Context = { InstrumentationKey = instrumentationKey }
                };

                telemetryProcessor.Process(request);
            }

            timeProvider.FastForward(TimeSpan.FromSeconds(30));

            for (int i = 0; i < 100; i++)
            {
                var request = new DependencyTelemetry()
                {
                    Success = i < 20,
                    Duration = TimeSpan.FromSeconds(counter++),
                    Context = { InstrumentationKey = instrumentationKey }
                };

                telemetryProcessor.Process(request);
            }

            // ASSERT
            Assert.AreEqual(0, errors.Length);

            var collectedTelemetryStreamAll =
                accumulatorManager.CurrentDataAccumulator.TelemetryDocuments.Where(document => document.DocumentStreamIds.Contains("StreamAll"))
                    .ToArray()
                    .Reverse()
                    .Cast<DependencyTelemetryDocument>()
                    .ToArray();

            var collectedTelemetryStreamSuccessOnly =
                accumulatorManager.CurrentDataAccumulator.TelemetryDocuments.Where(
                    document => document.DocumentStreamIds.Contains("StreamSuccessOnly"))
                    .ToArray()
                    .Reverse()
                    .Cast<DependencyTelemetryDocument>()
                    .ToArray();


            // the quota is 3 initially, then 0.5 every second (but not more than 30)

            // StreamAll has collected the initial quota of the first 100, then the additional accrued quota from the second 100
            Assert.AreEqual(3 + 15, collectedTelemetryStreamAll.Length);

            // out of the first 100 items we expect to see the initial quota of 3
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(i, collectedTelemetryStreamAll[i].Duration.TotalSeconds);
            }

            // out of the second 100 items we expect to see items 100 through 114 (the new quota for 30 seconds is 15)
            for (int i = 0; i < 15; i++)
            {
                Assert.AreEqual(100 + i, collectedTelemetryStreamAll[3 + i].Duration.TotalSeconds);
            }

            // StreamSuccessOnly never hit the quota during the first 100. It got 1 and had 2 quota left at the end of it. 
            // Out of the second 100, it got 2 that were left over in the quota + the newly accrued quota of 15
            Assert.AreEqual(1 + 17, collectedTelemetryStreamSuccessOnly.Length);

            // just one item of the first 100
            Assert.AreEqual(0, collectedTelemetryStreamSuccessOnly[0].Duration.TotalSeconds);

            // 17 (15 accrued quota + 2 left over quota) from the second 100
            for (int i = 0; i < 17; i++)
            {
                Assert.AreEqual(100 + i, collectedTelemetryStreamSuccessOnly[1 + i].Duration.TotalSeconds);
            }
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorDoesNotCollectFullExceptionTelemetryItemsOnceQuotaIsExhaustedIndependentlyPerDocumentStream()
        {
            // ARRANGE
            var documentStreamInfos = new[]
            {
                new DocumentStreamInfo()
                {
                    Id = "StreamAll",
                    DocumentFilterGroups =
                        new[]
                        {
                            new DocumentFilterConjunctionGroupInfo()
                            {
                                TelemetryType = TelemetryType.Exception,
                                Filters = new FilterConjunctionGroupInfo { Filters = new FilterInfo[0] }
                            }
                        }
                },
                new DocumentStreamInfo()
                {
                    Id = "StreamSuccessOnly",
                    DocumentFilterGroups =
                        new[]
                        {
                            new DocumentFilterConjunctionGroupInfo()
                            {
                                TelemetryType = TelemetryType.Exception,
                                Filters =
                                    new FilterConjunctionGroupInfo
                                    {
                                        Filters =
                                            new[] { new FilterInfo() { FieldName = "Message", Predicate = Predicate.Equal, Comparand = "true" } }
                                    }
                            }
                        }
                }
            };

            var collectionConfigurationInfo = new CollectionConfigurationInfo() { ETag = "ETag1", DocumentStreams = documentStreamInfos };


            var timeProvider = new ClockMock();
            var collectionConfiguration = new CollectionConfiguration(collectionConfigurationInfo, out errors, timeProvider);
            var accumulatorManager = new QuickPulseDataAccumulatorManager(collectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            var instrumentationKey = "some ikey";
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = instrumentationKey });

            // ACT
            int counter = 0;
            for (int i = 0; i < 100; i++)
            {
                var request = new ExceptionTelemetry()
                {
                    Exception = new Exception(i == 0 ? "true" : "false"),
                    Message = i == 0 ? "true" : "false",
                    Context = { InstrumentationKey = instrumentationKey, Operation = { Id = counter++.ToString() } }
                };

                telemetryProcessor.Process(request);
            }

            timeProvider.FastForward(TimeSpan.FromSeconds(30));

            for (int i = 0; i < 100; i++)
            {
                var request = new ExceptionTelemetry()
                {
                    Exception = new Exception(i < 20 ? "true" : "false"),
                    Message = i < 20 ? "true" : "false",
                    Context = { InstrumentationKey = instrumentationKey, Operation = { Id = counter++.ToString() } }
                };

                telemetryProcessor.Process(request);
            }

            // ASSERT
            Assert.AreEqual(0, errors.Length);

            var collectedTelemetryStreamAll =
                accumulatorManager.CurrentDataAccumulator.TelemetryDocuments.Where(document => document.DocumentStreamIds.Contains("StreamAll"))
                    .ToArray()
                    .Reverse()
                    .Cast<ExceptionTelemetryDocument>()
                    .ToArray();

            var collectedTelemetryStreamSuccessOnly =
                accumulatorManager.CurrentDataAccumulator.TelemetryDocuments.Where(
                    document => document.DocumentStreamIds.Contains("StreamSuccessOnly"))
                    .ToArray()
                    .Reverse()
                    .Cast<ExceptionTelemetryDocument>()
                    .ToArray();


            // the quota is 3 initially, then 0.5 every second (but not more than 30)

            // StreamAll has collected the initial quota of the first 100, then the additional accrued quota from the second 100
            Assert.AreEqual(3 + 15, collectedTelemetryStreamAll.Length);

            // out of the first 100 items we expect to see the initial quota of 3
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(i, int.Parse(collectedTelemetryStreamAll[i].OperationId));
            }

            // out of the second 100 items we expect to see items 100 through 114 (the new quota for 30 seconds is 15)
            for (int i = 0; i < 15; i++)
            {
                Assert.AreEqual(100 + i, int.Parse(collectedTelemetryStreamAll[3 + i].OperationId));
            }

            // StreamSuccessOnly never hit the quota during the first 100. It got 1 and had 2 quota left at the end of it. 
            // Out of the second 100, it got 2 that were left over in the quota + the newly accrued quota of 15
            Assert.AreEqual(1 + 17, collectedTelemetryStreamSuccessOnly.Length);

            // just one item of the first 100
            Assert.AreEqual(0, int.Parse(collectedTelemetryStreamSuccessOnly[0].OperationId));

            // 17 (15 accrued quota + 2 left over quota) from the second 100
            for (int i = 0; i < 17; i++)
            {
                Assert.AreEqual(100 + i, int.Parse(collectedTelemetryStreamSuccessOnly[1 + i].OperationId));
            }
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorDoesNotCollectFullEventTelemetryItemsOnceQuotaIsExhaustedIndependentlyPerDocumentStream()
        {
            // ARRANGE
            var documentStreamInfos = new[]
            {
                new DocumentStreamInfo()
                {
                    Id = "StreamAll",
                    DocumentFilterGroups =
                        new[]
                        {
                            new DocumentFilterConjunctionGroupInfo()
                            {
                                TelemetryType = TelemetryType.Event,
                                Filters = new FilterConjunctionGroupInfo { Filters = new FilterInfo[0] }
                            }
                        }
                },
                new DocumentStreamInfo()
                {
                    Id = "StreamSuccessOnly",
                    DocumentFilterGroups =
                        new[]
                        {
                            new DocumentFilterConjunctionGroupInfo()
                            {
                                TelemetryType = TelemetryType.Event,
                                Filters =
                                    new FilterConjunctionGroupInfo
                                    {
                                        Filters = new[] { new FilterInfo() { FieldName = "Name", Predicate = Predicate.Contains, Comparand = "true" } }
                                    }
                            }
                        }
                }
            };

            var collectionConfigurationInfo = new CollectionConfigurationInfo() { ETag = "ETag1", DocumentStreams = documentStreamInfos };


            var timeProvider = new ClockMock();
            var collectionConfiguration = new CollectionConfiguration(collectionConfigurationInfo, out errors, timeProvider);
            var accumulatorManager = new QuickPulseDataAccumulatorManager(collectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            var instrumentationKey = "some ikey";
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = instrumentationKey });

            // ACT
            int counter = 0;
            for (int i = 0; i < 100; i++)
            {
                var request = new EventTelemetry()
                {
                    Name = $"{(i == 0 ? "true" : "false")}#{counter ++}",
                    Context = { InstrumentationKey = instrumentationKey },
                };

                telemetryProcessor.Process(request);
            }

            timeProvider.FastForward(TimeSpan.FromSeconds(30));

            for (int i = 0; i < 100; i++)
            {
                var request = new EventTelemetry()
                {
                    Name = $"{(i < 20 ? "true" : "false")}#{counter++}",
                    Context = { InstrumentationKey = instrumentationKey }
                };

                telemetryProcessor.Process(request);
            }

            // ASSERT
            Assert.AreEqual(0, errors.Length);

            var collectedTelemetryStreamAll =
                accumulatorManager.CurrentDataAccumulator.TelemetryDocuments.Where(document => document.DocumentStreamIds.Contains("StreamAll"))
                    .ToArray()
                    .Reverse()
                    .Cast<EventTelemetryDocument>()
                    .ToArray();

            var collectedTelemetryStreamSuccessOnly =
                accumulatorManager.CurrentDataAccumulator.TelemetryDocuments.Where(
                    document => document.DocumentStreamIds.Contains("StreamSuccessOnly"))
                    .ToArray()
                    .Reverse()
                    .Cast<EventTelemetryDocument>()
                    .ToArray();


            // the quota is 3 initially, then 0.5 every second (but not more than 30)

            // StreamAll has collected the initial quota of the first 100, then the additional accrued quota from the second 100
            Assert.AreEqual(3 + 15, collectedTelemetryStreamAll.Length);

            // out of the first 100 items we expect to see the initial quota of 3
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(i, int.Parse(collectedTelemetryStreamAll[i].Name.Split('#')[1]));
            }

            // out of the second 100 items we expect to see items 100 through 114 (the new quota for 30 seconds is 15)
            for (int i = 0; i < 15; i++)
            {
                Assert.AreEqual(100 + i, int.Parse(collectedTelemetryStreamAll[3 + i].Name.Split('#')[1]));
            }

            // StreamSuccessOnly never hit the quota during the first 100. It got 1 and had 2 quota left at the end of it. 
            // Out of the second 100, it got 2 that were left over in the quota + the newly accrued quota of 15
            Assert.AreEqual(1 + 17, collectedTelemetryStreamSuccessOnly.Length);

            // just one item of the first 100
            Assert.AreEqual(0, int.Parse(collectedTelemetryStreamSuccessOnly[0].Name.Split('#')[1]));

            // 17 (15 accrued quota + 2 left over quota) from the second 100
            for (int i = 0; i < 17; i++)
            {
                Assert.AreEqual(100 + i, int.Parse(collectedTelemetryStreamSuccessOnly[1 + i].Name.Split('#')[1]));
            }
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorDoesNotCollectFullTraceTelemetryItemsOnceQuotaIsExhaustedIndependentlyPerDocumentStream()
        {
            // ARRANGE
            var documentStreamInfos = new[]
            {
                new DocumentStreamInfo()
                {
                    Id = "StreamAll",
                    DocumentFilterGroups =
                        new[]
                        {
                            new DocumentFilterConjunctionGroupInfo()
                            {
                                TelemetryType = TelemetryType.Trace,
                                Filters = new FilterConjunctionGroupInfo { Filters = new FilterInfo[0] }
                            }
                        }
                },
                new DocumentStreamInfo()
                {
                    Id = "StreamSuccessOnly",
                    DocumentFilterGroups =
                        new[]
                        {
                            new DocumentFilterConjunctionGroupInfo()
                            {
                                TelemetryType = TelemetryType.Trace,
                                Filters =
                                    new FilterConjunctionGroupInfo
                                    {
                                        Filters = new[] { new FilterInfo() { FieldName = "Message", Predicate = Predicate.Contains, Comparand = "true" } }
                                    }
                            }
                        }
                }
            };

            var collectionConfigurationInfo = new CollectionConfigurationInfo() { ETag = "ETag1", DocumentStreams = documentStreamInfos };


            var timeProvider = new ClockMock();
            var collectionConfiguration = new CollectionConfiguration(collectionConfigurationInfo, out errors, timeProvider);
            var accumulatorManager = new QuickPulseDataAccumulatorManager(collectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            var instrumentationKey = "some ikey";
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = instrumentationKey });

            // ACT
            int counter = 0;
            for (int i = 0; i < 100; i++)
            {
                var request = new TraceTelemetry()
                {
                    Message = $"{(i == 0 ? "true" : "false")}#{counter++}",
                    Context = { InstrumentationKey = instrumentationKey },
                };

                telemetryProcessor.Process(request);
            }

            timeProvider.FastForward(TimeSpan.FromSeconds(30));

            for (int i = 0; i < 100; i++)
            {
                var request = new TraceTelemetry()
                {
                    Message = $"{(i < 20 ? "true" : "false")}#{counter++}",
                    Context = { InstrumentationKey = instrumentationKey }
                };

                telemetryProcessor.Process(request);
            }

            // ASSERT
            Assert.AreEqual(0, errors.Length);

            var collectedTelemetryStreamAll =
                accumulatorManager.CurrentDataAccumulator.TelemetryDocuments.Where(document => document.DocumentStreamIds.Contains("StreamAll"))
                    .ToArray()
                    .Reverse()
                    .Cast<TraceTelemetryDocument>()
                    .ToArray();

            var collectedTelemetryStreamSuccessOnly =
                accumulatorManager.CurrentDataAccumulator.TelemetryDocuments.Where(
                    document => document.DocumentStreamIds.Contains("StreamSuccessOnly"))
                    .ToArray()
                    .Reverse()
                    .Cast<TraceTelemetryDocument>()
                    .ToArray();


            // the quota is 3 initially, then 0.5 every second (but not more than 30)

            // StreamAll has collected the initial quota of the first 100, then the additional accrued quota from the second 100
            Assert.AreEqual(3 + 15, collectedTelemetryStreamAll.Length);

            // out of the first 100 items we expect to see the initial quota of 3
            for (int i = 0; i < 3; i++)
            {
                Assert.AreEqual(i, int.Parse(collectedTelemetryStreamAll[i].Message.Split('#')[1]));
            }

            // out of the second 100 items we expect to see items 100 through 114 (the new quota for 30 seconds is 15)
            for (int i = 0; i < 15; i++)
            {
                Assert.AreEqual(100 + i, int.Parse(collectedTelemetryStreamAll[3 + i].Message.Split('#')[1]));
            }

            // StreamSuccessOnly never hit the quota during the first 100. It got 1 and had 2 quota left at the end of it. 
            // Out of the second 100, it got 2 that were left over in the quota + the newly accrued quota of 15
            Assert.AreEqual(1 + 17, collectedTelemetryStreamSuccessOnly.Length);

            // just one item of the first 100
            Assert.AreEqual(0, int.Parse(collectedTelemetryStreamSuccessOnly[0].Message.Split('#')[1]));

            // 17 (15 accrued quota + 2 left over quota) from the second 100
            for (int i = 0; i < 17; i++)
            {
                Assert.AreEqual(100 + i, int.Parse(collectedTelemetryStreamSuccessOnly[1 + i].Message.Split('#')[1]));
            }
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorDoesNotCollectFullTelemetryItemsOnceGlobalQuotaIsExhausted()
        {
            // ARRANGE
            var documentStreamInfos = new List<DocumentStreamInfo>();

            // we have 15 streams (global quota is 10 * 30 documents per minute (5 documents per second), which is 10x the per-stream quota
            var streamCount = 15;
            for (int i = 0; i < streamCount; i ++)
            {
                documentStreamInfos.Add(
                    new DocumentStreamInfo()
                    {
                        Id = $"Stream{i}#",
                        DocumentFilterGroups =
                            new[]
                            {
                                new DocumentFilterConjunctionGroupInfo()
                                {
                                    TelemetryType = TelemetryType.Request,
                                    Filters = new FilterConjunctionGroupInfo { Filters = new FilterInfo[0] }
                                }
                            }
                    });
            }

            var collectionConfigurationInfo = new CollectionConfigurationInfo() { ETag = "ETag1", DocumentStreams = documentStreamInfos.ToArray() };

            var timeProvider = new ClockMock();
            var collectionConfiguration = new CollectionConfiguration(collectionConfigurationInfo, out errors, timeProvider);
            var accumulatorManager = new QuickPulseDataAccumulatorManager(collectionConfiguration);

            float maxGlobalTelemetryQuota = 6;
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy(), timeProvider, maxGlobalTelemetryQuota, 0);
            var instrumentationKey = "some ikey";
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = instrumentationKey });

            // ACT
            // accrue the full quota (6 per minute for the purpose of this test, which is 0.1 per second)
            timeProvider.FastForward(TimeSpan.FromHours(1));

            // push 10 items to each stream
            for (int i = 0; i < 10; i ++)
            {
                telemetryProcessor.Process(new RequestTelemetry() { Name = i.ToString(), Context = { InstrumentationKey = "some ikey" } });
            }

            // ASSERT
            Assert.AreEqual(0, errors.Length);

            Assert.IsTrue(accumulatorManager.CurrentDataAccumulator.GlobalDocumentQuotaReached);

            // we expect to see the first 6 documents in each stream, which is the global quota
            Assert.AreEqual(maxGlobalTelemetryQuota, accumulatorManager.CurrentDataAccumulator.TelemetryDocuments.Count);

            for (int i = 0; i < streamCount; i ++)
            {
                var streamId = $"Stream{i}#";
                var collectedTelemetryForStream =
                    accumulatorManager.CurrentDataAccumulator.TelemetryDocuments.Where(document => document.DocumentStreamIds.Contains(streamId))
                        .ToArray()
                        .Reverse()
                        .Cast<RequestTelemetryDocument>()
                        .ToArray();

                Assert.AreEqual(maxGlobalTelemetryQuota, collectedTelemetryForStream.Length);

                for (int j = 0; j < collectedTelemetryForStream.Length; j ++)
                {
                    Assert.AreEqual(j, int.Parse(collectedTelemetryForStream[j].Name));
                }
            }
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorDoesNotCollectFullTelemetryItemsWhenSwitchIsOff()
        {
            // ARRANGE
            var accumulatorManager = new QuickPulseDataAccumulatorManager(EmptyCollectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            var instrumentationKey = "some ikey";
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = instrumentationKey },
                disableFullTelemetryItems: true);

            // ACT
            var request = new RequestTelemetry()
            {
                Success = false,
                ResponseCode = "500",
                Duration = TimeSpan.FromSeconds(1),
                Context = { InstrumentationKey = instrumentationKey }
            };

            var dependency = new DependencyTelemetry()
            {
                Success = false,
                Duration = TimeSpan.FromSeconds(1),
                Context = { InstrumentationKey = instrumentationKey }
            };

            var exception = new ExceptionTelemetry(new ArgumentException("bla")) { Context = { InstrumentationKey = instrumentationKey } };

            var @event = new EventTelemetry() { Context = { InstrumentationKey = instrumentationKey } };

            telemetryProcessor.Process(request);
            telemetryProcessor.Process(dependency);
            telemetryProcessor.Process(exception);
            telemetryProcessor.Process(@event);

            // ASSERT
            Assert.AreEqual(0, accumulatorManager.CurrentDataAccumulator.TelemetryDocuments.Count);
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorTruncatesLongFullRequestTelemetryItemName()
        {
            // ARRANGE
            var documentStreamInfo = new DocumentStreamInfo()
            {
                Id = "Stream1",
                DocumentFilterGroups =
                  new[]
                  {
                        new DocumentFilterConjunctionGroupInfo()
                        {
                            TelemetryType = TelemetryType.Request,
                            Filters = new FilterConjunctionGroupInfo { Filters = new FilterInfo[0] }
                        },
                  }
            };

            var collectionConfigurationInfo = new CollectionConfigurationInfo()
            {
                DocumentStreams = new[] { documentStreamInfo },
                ETag = "ETag1"
            };

            var collectionConfiguration = new CollectionConfiguration(collectionConfigurationInfo, out errors, new ClockMock());

            var accumulatorManager = new QuickPulseDataAccumulatorManager(collectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            var instrumentationKey = "some ikey";
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = instrumentationKey });

            // ACT
            var requestShort = new RequestTelemetry(new string('r', MaxFieldLength), DateTimeOffset.Now, TimeSpan.FromSeconds(1), "500", false)
            {
                Context = { InstrumentationKey = instrumentationKey }
            };
            var requestLong = new RequestTelemetry(new string('r', MaxFieldLength + 1), DateTimeOffset.Now, TimeSpan.FromSeconds(1), "500", false)
            {
                Context = { InstrumentationKey = instrumentationKey }
            };

            // process in the opposite order to allow for an easier validation order
            telemetryProcessor.Process(requestLong);
            telemetryProcessor.Process(requestShort);

            // ASSERT
            var telemetryDocuments = accumulatorManager.CurrentDataAccumulator.TelemetryDocuments.Cast<RequestTelemetryDocument>().ToList();

            Assert.AreEqual(telemetryDocuments[0].Name, requestShort.Name);
            Assert.AreEqual(telemetryDocuments[1].Name, requestShort.Name);
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorTruncatesLongFullRequestTelemetryItemProperties()
        {
            // ARRANGE
            var documentStreamInfo = new DocumentStreamInfo()
            {
                Id = "Stream1",
                DocumentFilterGroups =
                  new[]
                  {
                        new DocumentFilterConjunctionGroupInfo()
                        {
                            TelemetryType = TelemetryType.Request,
                            Filters = new FilterConjunctionGroupInfo { Filters = new FilterInfo[0] }
                        },
                  }
            };

            var collectionConfigurationInfo = new CollectionConfigurationInfo()
            {
                DocumentStreams = new[] { documentStreamInfo },
                ETag = "ETag1"
            };

            var collectionConfiguration = new CollectionConfiguration(collectionConfigurationInfo, out errors, new ClockMock());

            var accumulatorManager = new QuickPulseDataAccumulatorManager(collectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            var instrumentationKey = "some ikey";
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = instrumentationKey });

            // ACT
            var requestShort = new RequestTelemetry("requestShort", DateTimeOffset.Now, TimeSpan.FromSeconds(1), "500", false)
            {
                Properties = { { new string('p', MaxFieldLength), new string('v', MaxFieldLength) } },
                Context = { InstrumentationKey = instrumentationKey }
            };

            var requestLong = new RequestTelemetry("requestLong", DateTimeOffset.Now, TimeSpan.FromSeconds(1), "500", false)
            {
                Properties = { { new string('p', MaxFieldLength + 1), new string('v', MaxFieldLength + 1) } },
                Context = { InstrumentationKey = instrumentationKey }
            };

            // process in the opposite order to allow for an easier validation order
            telemetryProcessor.Process(requestLong);
            telemetryProcessor.Process(requestShort);

            // ASSERT
            var telemetryDocuments = accumulatorManager.CurrentDataAccumulator.TelemetryDocuments.Cast<RequestTelemetryDocument>().ToList();

            var actual = telemetryDocuments[0].Properties.First();
            var expected = requestShort.Properties.First();
            Assert.AreEqual(expected.Key, actual.Key);
            Assert.AreEqual(expected.Value, actual.Value);

            actual = telemetryDocuments[1].Properties.First();
            Assert.AreEqual(expected.Key, actual.Key);
            Assert.AreEqual(expected.Value, actual.Value);
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorTruncatesLongFullDependencyTelemetryItemCommandName()
        {
            // ARRANGE
            var documentStreamInfo = new DocumentStreamInfo()
            {
                Id = "Stream1",
                DocumentFilterGroups =
                  new[]
                  {
                        new DocumentFilterConjunctionGroupInfo()
                        {
                            TelemetryType = TelemetryType.Dependency,
                            Filters = new FilterConjunctionGroupInfo { Filters = new FilterInfo[0] }
                        },
                  }
            };

            var collectionConfigurationInfo = new CollectionConfigurationInfo()
            {
                DocumentStreams = new[] { documentStreamInfo },
                ETag = "ETag1"
            };

            var collectionConfiguration = new CollectionConfiguration(collectionConfigurationInfo, out errors, new ClockMock());

            var accumulatorManager = new QuickPulseDataAccumulatorManager(collectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            var instrumentationKey = "some ikey";
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = instrumentationKey });

            // ACT
            var dependencyShort = new DependencyTelemetry(
                new string('c', MaxFieldLength),
                new string('c', MaxFieldLength),
                new string('c', MaxFieldLength),
                new string('c', MaxFieldLength),
                DateTimeOffset.Now,
                TimeSpan.FromSeconds(1),
                new string('c', MaxFieldLength),
                false) { Context = { InstrumentationKey = instrumentationKey } };

            var dependencyLong = new DependencyTelemetry(
                new string('c', MaxFieldLength + 1),
                new string('c', MaxFieldLength + 1),
                new string('c', MaxFieldLength + 1),
                new string('c', MaxFieldLength + 1),
                DateTimeOffset.Now,
                TimeSpan.FromSeconds(1),
                new string('c', MaxFieldLength + 1),
                false) { Context = { InstrumentationKey = instrumentationKey } };

            // process in the opposite order to allow for an easier validation order
            telemetryProcessor.Process(dependencyLong);
            telemetryProcessor.Process(dependencyShort);

            // ASSERT
            var telemetryDocuments = accumulatorManager.CurrentDataAccumulator.TelemetryDocuments.Cast<DependencyTelemetryDocument>().ToList();

            Assert.AreEqual(telemetryDocuments[0].CommandName, dependencyShort.Data);
            Assert.AreEqual(telemetryDocuments[1].CommandName, dependencyLong.Data.Substring(0, MaxFieldLength));
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorTruncatesLongFullDependencyTelemetryItemName()
        {
            // ARRANGE
            var documentStreamInfo = new DocumentStreamInfo()
            {
                Id = "Stream1",
                DocumentFilterGroups =
                  new[]
                  {
                        new DocumentFilterConjunctionGroupInfo()
                        {
                            TelemetryType = TelemetryType.Dependency,
                            Filters = new FilterConjunctionGroupInfo { Filters = new FilterInfo[0] }
                        },
                  }
            };

            var collectionConfigurationInfo = new CollectionConfigurationInfo()
            {
                DocumentStreams = new[] { documentStreamInfo },
                ETag = "ETag1"
            };

            var collectionConfiguration = new CollectionConfiguration(collectionConfigurationInfo, out errors, new ClockMock());

            var accumulatorManager = new QuickPulseDataAccumulatorManager(collectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            var instrumentationKey = "some ikey";
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = instrumentationKey });

            // ACT
            var dependencyShort = new DependencyTelemetry(
                new string('c', MaxFieldLength),
                new string('c', MaxFieldLength),
                new string('c', MaxFieldLength),
                new string('c', MaxFieldLength),
                DateTimeOffset.Now,
                TimeSpan.FromSeconds(1),
                new string('c', MaxFieldLength),
                false) { Context = { InstrumentationKey = instrumentationKey } };

            var dependencyLong = new DependencyTelemetry(
                new string('c', MaxFieldLength + 1),
                new string('c', MaxFieldLength + 1),
                new string('c', MaxFieldLength + 1),
                new string('c', MaxFieldLength + 1),
                DateTimeOffset.Now,
                TimeSpan.FromSeconds(1),
                new string('c', MaxFieldLength + 1),
                false) { Context = { InstrumentationKey = instrumentationKey } };

            // process in the opposite order to allow for an easier validation order
            telemetryProcessor.Process(dependencyLong);
            telemetryProcessor.Process(dependencyShort);

            // ASSERT
            var telemetryDocuments = accumulatorManager.CurrentDataAccumulator.TelemetryDocuments.Cast<DependencyTelemetryDocument>().ToList();

            Assert.AreEqual(telemetryDocuments[0].Name, dependencyShort.Name);
            Assert.AreEqual(telemetryDocuments[1].Name, dependencyLong.Name.Substring(0, MaxFieldLength));
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorTruncatesLongFullDependencyTelemetryItemProperties()
        {
            // ARRANGE
            var documentStreamInfo = new DocumentStreamInfo()
            {
                Id = "Stream1",
                DocumentFilterGroups =
                  new[]
                  {
                        new DocumentFilterConjunctionGroupInfo()
                        {
                            TelemetryType = TelemetryType.Dependency,
                            Filters = new FilterConjunctionGroupInfo { Filters = new FilterInfo[0] }
                        },
                  }
            };

            var collectionConfigurationInfo = new CollectionConfigurationInfo()
            {
                DocumentStreams = new[] { documentStreamInfo },
                ETag = "ETag1"
            };

            var collectionConfiguration = new CollectionConfiguration(collectionConfigurationInfo, out errors, new ClockMock());

            var accumulatorManager = new QuickPulseDataAccumulatorManager(collectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            var instrumentationKey = "some ikey";
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = instrumentationKey });

            // ACT
            var dependencyShort = new DependencyTelemetry(
                "dependencyShort",
                "dependencyShort",
                "dependencyShort",
                "dependencyShort",
                DateTimeOffset.Now,
                TimeSpan.FromSeconds(1),
                "dependencyShort",
                false)
            {
                Properties = { { new string('p', MaxFieldLength), new string('v', MaxFieldLength) } },
                Context = { InstrumentationKey = instrumentationKey }
            };

            var dependencyLong = new DependencyTelemetry(
                "dependencyLong",
                "dependencyLong",
                "dependencyLong",
                "dependencyLong",
                DateTimeOffset.Now,
                TimeSpan.FromSeconds(1),
                "dependencyLong",
                false)
            {
                Properties = { { new string('p', MaxFieldLength + 1), new string('v', MaxFieldLength + 1) } },
                Context = { InstrumentationKey = instrumentationKey }
            };

            // process in the opposite order to allow for an easier validation order
            telemetryProcessor.Process(dependencyLong);
            telemetryProcessor.Process(dependencyShort);

            // ASSERT
            var telemetryDocuments = accumulatorManager.CurrentDataAccumulator.TelemetryDocuments.Cast<DependencyTelemetryDocument>().ToList();

            var expected = dependencyShort.Properties.First();
            var actual = telemetryDocuments[0].Properties.First();
            Assert.AreEqual(expected.Key, actual.Key);
            Assert.AreEqual(expected.Value, actual.Value);

            actual = telemetryDocuments[1].Properties.First();
            Assert.AreEqual(expected.Key, actual.Key);
            Assert.AreEqual(expected.Value, actual.Value);
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorTruncatesLongFullExceptionTelemetryItemMessage()
        {
            // ARRANGE
            var documentStreamInfo = new DocumentStreamInfo()
            {
                Id = "Stream1",
                DocumentFilterGroups =
                  new[]
                  {
                        new DocumentFilterConjunctionGroupInfo()
                        {
                            TelemetryType = TelemetryType.Exception,
                            Filters = new FilterConjunctionGroupInfo { Filters = new FilterInfo[0] }
                        },
                  }
            };

            var collectionConfigurationInfo = new CollectionConfigurationInfo()
            {
                DocumentStreams = new[] { documentStreamInfo },
                ETag = "ETag1"
            };

            var collectionConfiguration = new CollectionConfiguration(collectionConfigurationInfo, out errors, new ClockMock());

            var accumulatorManager = new QuickPulseDataAccumulatorManager(collectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            var instrumentationKey = "some ikey";
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = instrumentationKey });

            // ACT
            var exceptionShort = new ExceptionTelemetry(new ArgumentException(new string('m', MaxFieldLength)))
            {
                Context = { InstrumentationKey = instrumentationKey }
            };

            var exceptionLong = new ExceptionTelemetry(new ArgumentException(new string('m', MaxFieldLength + 1)))
            {
                Context = { InstrumentationKey = instrumentationKey }
            };

            // process in the opposite order to allow for an easier validation order
            telemetryProcessor.Process(exceptionLong);
            telemetryProcessor.Process(exceptionShort);

            // ASSERT
            var telemetryDocuments = accumulatorManager.CurrentDataAccumulator.TelemetryDocuments.Cast<ExceptionTelemetryDocument>().ToList();

            Assert.AreEqual(telemetryDocuments[0].ExceptionMessage, exceptionShort.Exception.Message);
            Assert.AreEqual(telemetryDocuments[1].ExceptionMessage, exceptionLong.Exception.Message.Substring(0, MaxFieldLength));
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorTruncatesLongFullExceptionTelemetryItemProperties()
        {
            // ARRANGE
            var documentStreamInfo = new DocumentStreamInfo()
            {
                Id = "Stream1",
                DocumentFilterGroups =
                  new[]
                  {
                        new DocumentFilterConjunctionGroupInfo()
                        {
                            TelemetryType = TelemetryType.Exception,
                            Filters = new FilterConjunctionGroupInfo { Filters = new FilterInfo[0] }
                        },
                  }
            };

            var collectionConfigurationInfo = new CollectionConfigurationInfo()
            {
                DocumentStreams = new[] { documentStreamInfo },
                ETag = "ETag1"
            };

            var collectionConfiguration = new CollectionConfiguration(collectionConfigurationInfo, out errors, new ClockMock());

            var accumulatorManager = new QuickPulseDataAccumulatorManager(collectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            var instrumentationKey = "some ikey";
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = instrumentationKey });

            // ACT
            var exceptionShort = new ExceptionTelemetry(new ArgumentException())
            {
                Properties = { { new string('p', MaxFieldLength), new string('v', MaxFieldLength) } },
                Message = new string('m', MaxFieldLength),
                Context = { InstrumentationKey = instrumentationKey }
            };

            var exceptionLong = new ExceptionTelemetry(new ArgumentException())
            {
                Properties = { { new string('p', MaxFieldLength + 1), new string('v', MaxFieldLength + 1) } },
                Message = new string('m', MaxFieldLength),
                Context = { InstrumentationKey = instrumentationKey }
            };

            // process in the opposite order to allow for an easier validation order
            telemetryProcessor.Process(exceptionLong);
            telemetryProcessor.Process(exceptionShort);

            // ASSERT
            var telemetryDocuments = accumulatorManager.CurrentDataAccumulator.TelemetryDocuments.Cast<ExceptionTelemetryDocument>().ToList();

            var expected = exceptionShort.Properties.First();
            var actual = telemetryDocuments[0].Properties.First();
            Assert.AreEqual(expected.Key, actual.Key);
            Assert.AreEqual(expected.Value, actual.Value);

            actual = telemetryDocuments[1].Properties.First();
            Assert.AreEqual(expected.Key, actual.Key);
            Assert.AreEqual(expected.Value, actual.Value);
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorHandlesDuplicatePropertyNamesDueToTruncation()
        {
            // ARRANGE
            var documentStreamInfo = new DocumentStreamInfo()
            {
                Id = "Stream1",
                DocumentFilterGroups =
                  new[]
                  {
                        new DocumentFilterConjunctionGroupInfo()
                        {
                            TelemetryType = TelemetryType.Exception,
                            Filters = new FilterConjunctionGroupInfo { Filters = new FilterInfo[0] }
                        },
                  }
            };

            var collectionConfigurationInfo = new CollectionConfigurationInfo()
            {
                DocumentStreams = new[] { documentStreamInfo },
                ETag = "ETag1"
            };

            var collectionConfiguration = new CollectionConfiguration(collectionConfigurationInfo, out errors, new ClockMock());

            var accumulatorManager = new QuickPulseDataAccumulatorManager(collectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            var instrumentationKey = "some ikey";
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = instrumentationKey });

            // ACT
            var exception = new ExceptionTelemetry(new ArgumentException())
            {
                Properties = { { new string('p', MaxFieldLength + 1), "Val1" }, { new string('p', MaxFieldLength + 2), "Val2" } },
                Message = "Message",
                Context = { InstrumentationKey = instrumentationKey }
            };

            telemetryProcessor.Process(exception);

            // ASSERT
            var telemetryDocuments = accumulatorManager.CurrentDataAccumulator.TelemetryDocuments.Cast<ExceptionTelemetryDocument>().ToList();

            Assert.AreEqual(1, telemetryDocuments[0].Properties.Length);
            Assert.AreEqual(new string('p', MaxFieldLength), telemetryDocuments[0].Properties.First().Key);
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorExpandsAggregateExceptionMessage()
        {
            // ARRANGE
            var documentStreamInfo = new DocumentStreamInfo()
            {
                Id = "Stream1",
                DocumentFilterGroups =
                  new[]
                  {
                        new DocumentFilterConjunctionGroupInfo()
                        {
                            TelemetryType = TelemetryType.Exception,
                            Filters = new FilterConjunctionGroupInfo { Filters = new FilterInfo[0] }
                        },
                  }
            };

            var collectionConfigurationInfo = new CollectionConfigurationInfo()
            {
                DocumentStreams = new[] { documentStreamInfo },
                ETag = "ETag1"
            };

            var collectionConfiguration = new CollectionConfiguration(collectionConfigurationInfo, out errors, new ClockMock());

            var accumulatorManager = new QuickPulseDataAccumulatorManager(collectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            var instrumentationKey = "some ikey";
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = instrumentationKey });

            var exception1 = new Exception("Exception 1");
            var exception2 = new Exception("Exception 2");
            var exception3 = new AggregateException("Exception 3", new Exception("Exception 4"), new Exception("Exception 5"));

            var aggregateException = new AggregateException("Top level message", exception1, exception2, exception3);

            // ACT
            var exceptionTelemetry = new ExceptionTelemetry(aggregateException) { Context = { InstrumentationKey = instrumentationKey } };

            telemetryProcessor.Process(exceptionTelemetry);

            // ASSERT
            var telemetryDocuments = accumulatorManager.CurrentDataAccumulator.TelemetryDocuments.Cast<ExceptionTelemetryDocument>().ToList();

            Assert.AreEqual("Exception 1 <--- Exception 2 <--- Exception 4 <--- Exception 5", telemetryDocuments[0].ExceptionMessage);
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorExpandsAggregateExceptionMessageWhenEmpty()
        {
            // ARRANGE
            var documentStreamInfo = new DocumentStreamInfo()
            {
                Id = "Stream1",
                DocumentFilterGroups =
                  new[]
                  {
                        new DocumentFilterConjunctionGroupInfo()
                        {
                            TelemetryType = TelemetryType.Exception,
                            Filters = new FilterConjunctionGroupInfo { Filters = new FilterInfo[0] }
                        },
                  }
            };

            var collectionConfigurationInfo = new CollectionConfigurationInfo()
            {
                DocumentStreams = new[] { documentStreamInfo },
                ETag = "ETag1"
            };

            var collectionConfiguration = new CollectionConfiguration(collectionConfigurationInfo, out errors, new ClockMock());

            var accumulatorManager = new QuickPulseDataAccumulatorManager(collectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            var instrumentationKey = "some ikey";
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = instrumentationKey });

            var exception = new AggregateException(string.Empty);

            // ACT
            var exceptionTelemetry = new ExceptionTelemetry(exception) { Context = { InstrumentationKey = instrumentationKey } };

            telemetryProcessor.Process(exceptionTelemetry);

            // ASSERT
            var telemetryDocuments = accumulatorManager.CurrentDataAccumulator.TelemetryDocuments.Cast<ExceptionTelemetryDocument>().ToList();

            Assert.AreEqual(string.Empty, telemetryDocuments[0].ExceptionMessage);
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorExpandsExceptionMessageWhenSingleInnerException()
        {
            // ARRANGE
            var documentStreamInfo = new DocumentStreamInfo()
            {
                Id = "Stream1",
                DocumentFilterGroups =
                  new[]
                  {
                        new DocumentFilterConjunctionGroupInfo()
                        {
                            TelemetryType = TelemetryType.Exception,
                            Filters = new FilterConjunctionGroupInfo { Filters = new FilterInfo[0] }
                        },
                  }
            };

            var collectionConfigurationInfo = new CollectionConfigurationInfo()
            {
                DocumentStreams = new[] { documentStreamInfo },
                ETag = "ETag1"
            };

            var collectionConfiguration = new CollectionConfiguration(collectionConfigurationInfo, out errors, new ClockMock());

            var accumulatorManager = new QuickPulseDataAccumulatorManager(collectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            var instrumentationKey = "some ikey";
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = instrumentationKey });

            var exception = new Exception("Exception 1", new Exception("Exception 2"));

            // ACT
            var exceptionTelemetry = new ExceptionTelemetry(exception) { Context = { InstrumentationKey = instrumentationKey } };

            telemetryProcessor.Process(exceptionTelemetry);

            // ASSERT
            var telemetryDocuments = accumulatorManager.CurrentDataAccumulator.TelemetryDocuments.Cast<ExceptionTelemetryDocument>().ToList();

            Assert.AreEqual("Exception 1 <--- Exception 2", telemetryDocuments[0].ExceptionMessage);
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorExpandsExceptionMessageWhenNoInnerExceptions()
        {
            // ARRANGE
            var documentStreamInfo = new DocumentStreamInfo()
            {
                Id = "Stream1",
                DocumentFilterGroups =
                  new[]
                  {
                        new DocumentFilterConjunctionGroupInfo()
                        {
                            TelemetryType = TelemetryType.Exception,
                            Filters = new FilterConjunctionGroupInfo { Filters = new FilterInfo[0] }
                        },
                  }
            };

            var collectionConfigurationInfo = new CollectionConfigurationInfo()
            {
                DocumentStreams = new[] { documentStreamInfo },
                ETag = "ETag1"
            };

            var collectionConfiguration = new CollectionConfiguration(collectionConfigurationInfo, out errors, new ClockMock());

            var accumulatorManager = new QuickPulseDataAccumulatorManager(collectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            var instrumentationKey = "some ikey";
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = instrumentationKey });

            var exception = new Exception("Exception 1");

            // ACT
            var exceptionTelemetry = new ExceptionTelemetry(exception) { Context = { InstrumentationKey = instrumentationKey } };

            telemetryProcessor.Process(exceptionTelemetry);

            // ASSERT
            var telemetryDocuments = accumulatorManager.CurrentDataAccumulator.TelemetryDocuments.Cast<ExceptionTelemetryDocument>().ToList();

            Assert.AreEqual("Exception 1", telemetryDocuments[0].ExceptionMessage);
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorExpandsExceptionMessageWhenMultipleInnerExceptions()
        {
            // ARRANGE
            var documentStreamInfo = new DocumentStreamInfo()
            {
                Id = "Stream1",
                DocumentFilterGroups =
                  new[]
                  {
                        new DocumentFilterConjunctionGroupInfo()
                        {
                            TelemetryType = TelemetryType.Exception,
                            Filters = new FilterConjunctionGroupInfo { Filters = new FilterInfo[0] }
                        },
                  }
            };

            var collectionConfigurationInfo = new CollectionConfigurationInfo()
            {
                DocumentStreams = new[] { documentStreamInfo },
                ETag = "ETag1"
            };

            var collectionConfiguration = new CollectionConfiguration(collectionConfigurationInfo, out errors, new ClockMock());

            var accumulatorManager = new QuickPulseDataAccumulatorManager(collectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            var instrumentationKey = "some ikey";
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = instrumentationKey });

            var exception = new Exception("Exception 1", new Exception("Exception 2", new Exception("Exception 3")));

            // ACT
            var exceptionTelemetry = new ExceptionTelemetry(exception) { Context = { InstrumentationKey = instrumentationKey } };

            telemetryProcessor.Process(exceptionTelemetry);

            // ASSERT
            var telemetryDocuments = accumulatorManager.CurrentDataAccumulator.TelemetryDocuments.Cast<ExceptionTelemetryDocument>().ToList();

            Assert.AreEqual("Exception 1 <--- Exception 2 <--- Exception 3", telemetryDocuments[0].ExceptionMessage);
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorExpandsExceptionMessagesAndDedupesThem()
        {
            // ARRANGE
            var documentStreamInfo = new DocumentStreamInfo()
            {
                Id = "Stream1",
                DocumentFilterGroups =
                  new[]
                  {
                        new DocumentFilterConjunctionGroupInfo()
                        {
                            TelemetryType = TelemetryType.Exception,
                            Filters = new FilterConjunctionGroupInfo { Filters = new FilterInfo[0] }
                        },
                  }
            };

            var collectionConfigurationInfo = new CollectionConfigurationInfo()
            {
                DocumentStreams = new[] { documentStreamInfo },
                ETag = "ETag1"
            };

            var collectionConfiguration = new CollectionConfiguration(collectionConfigurationInfo, out errors, new ClockMock());

            var accumulatorManager = new QuickPulseDataAccumulatorManager(collectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            var instrumentationKey = "some ikey";
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = instrumentationKey });

            var exception = new AggregateException(
                "Exception 1",
                new Exception("Exception 1", new Exception("Exception 1")),
                new Exception("Exception 1"));

            // ACT
            var exceptionTelemetry = new ExceptionTelemetry(exception) { Context = { InstrumentationKey = instrumentationKey } };

            telemetryProcessor.Process(exceptionTelemetry);

            // ASSERT
            var telemetryDocuments = accumulatorManager.CurrentDataAccumulator.TelemetryDocuments.Cast<ExceptionTelemetryDocument>().ToList();

            Assert.AreEqual("Exception 1", telemetryDocuments[0].ExceptionMessage);
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorCalculatesOperationalizedMetricsForRequests()
        {
            // ARRANGE
            var filterInfoResponseCodeGreaterThanOrEqualTo500 = new FilterInfo()
            {
                FieldName = "ResponseCode",
                Predicate = Predicate.GreaterThanOrEqual,
                Comparand = "500"
            };
            var filterInfoResponseCode200 = new FilterInfo() { FieldName = "ResponseCode", Predicate = Predicate.Equal, Comparand = "201" };
            var filterInfoSuccessful = new FilterInfo() { FieldName = "Success", Predicate = Predicate.Equal, Comparand = "true" };
            var filterInfoFailed = new FilterInfo() { FieldName = "Success", Predicate = Predicate.Equal, Comparand = "false" };

            var metrics = new[]
            {
                new OperationalizedMetricInfo()
                {
                    Id = "AverageIdOfFailedRequestsGreaterThanOrEqualTo500",
                    TelemetryType = TelemetryType.Request,
                    Projection = "Id",
                    Aggregation = AggregationType.Avg,
                    FilterGroups =
                        new[]
                        { new FilterConjunctionGroupInfo() { Filters = new[] { filterInfoResponseCodeGreaterThanOrEqualTo500, filterInfoFailed } } }
                },
                new OperationalizedMetricInfo()
                {
                    Id = "SumIdsOfSuccessfulRequestsEqualTo201",
                    TelemetryType = TelemetryType.Request,
                    Projection = "Id",
                    Aggregation = AggregationType.Sum,
                    FilterGroups = new[] { new FilterConjunctionGroupInfo() { Filters = new[] { filterInfoResponseCode200, filterInfoSuccessful } } }
                }
            };

            var collectionConfiguration = new CollectionConfiguration(new CollectionConfigurationInfo() { Metrics = metrics }, out errors, new ClockMock());
            var accumulatorManager = new QuickPulseDataAccumulatorManager(collectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            var instrumentationKey = "some ikey";
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = instrumentationKey });

            // ACT
            var requests = new[]
            {
                new RequestTelemetry() { Id = "1", Success = true, ResponseCode = "500" },
                new RequestTelemetry() { Id = "2", Success = false, ResponseCode = "500" },
                new RequestTelemetry() { Id = "3", Success = true, ResponseCode = "501" },
                new RequestTelemetry() { Id = "4", Success = false, ResponseCode = "501" },
                new RequestTelemetry() { Id = "5", Success = true, ResponseCode = "499" },
                new RequestTelemetry() { Id = "6", Success = false, ResponseCode = "499" },
                new RequestTelemetry() { Id = "7", Success = true, ResponseCode = "201" },
                new RequestTelemetry() { Id = "8", Success = false, ResponseCode = "201" },
                new RequestTelemetry() { Id = "9", Success = true, ResponseCode = "blah" },
                new RequestTelemetry() { Id = "10", Success = false, ResponseCode = "blah" },
            };

            Array.ForEach(requests, r => r.Context.InstrumentationKey = instrumentationKey);

            Array.ForEach(requests, telemetryProcessor.Process);

            // ASSERT
            Dictionary<string, AccumulatedValue> calculatedMetrics =
                accumulatorManager.CurrentDataAccumulator.CollectionConfigurationAccumulator.MetricAccumulators;

            Assert.AreEqual(2, calculatedMetrics.Count);

            Assert.AreEqual(
                "2, 4",
                string.Join(", ", calculatedMetrics["AverageIdOfFailedRequestsGreaterThanOrEqualTo500"].Value.Reverse().ToArray()));
            Assert.AreEqual("7", string.Join(", ", calculatedMetrics["SumIdsOfSuccessfulRequestsEqualTo201"].Value.Reverse().ToArray()));
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorCalculatesOperationalizedMetricsForDependencies()
        {
            // ARRANGE
            var filterInfoDataGreaterThanOrEqualTo500 = new FilterInfo()
            {
                FieldName = "Data",
                Predicate = Predicate.GreaterThanOrEqual,
                Comparand = "500"
            };
            var filterInfoData200 = new FilterInfo() { FieldName = "Data", Predicate = Predicate.Equal, Comparand = "201" };
            var filterInfoSuccessful = new FilterInfo() { FieldName = "Success", Predicate = Predicate.Equal, Comparand = "true" };
            var filterInfoFailed = new FilterInfo() { FieldName = "Success", Predicate = Predicate.Equal, Comparand = "false" };

            var metrics = new[]
            {
                new OperationalizedMetricInfo()
                {
                    Id = "AverageIdOfFailedDependenciesGreaterThanOrEqualTo500",
                    TelemetryType = TelemetryType.Dependency,
                    Projection = "Id",
                    Aggregation = AggregationType.Avg,
                    FilterGroups =
                        new[] { new FilterConjunctionGroupInfo() { Filters = new[] { filterInfoDataGreaterThanOrEqualTo500, filterInfoFailed } } }
                },
                new OperationalizedMetricInfo()
                {
                    Id = "SumIdsOfSuccessfulDependenciesEqualTo201",
                    TelemetryType = TelemetryType.Dependency,
                    Projection = "Id",
                    Aggregation = AggregationType.Sum,
                    FilterGroups = new[] { new FilterConjunctionGroupInfo() { Filters = new[] { filterInfoData200, filterInfoSuccessful } } }
                }
            };

            var collectionConfiguration = new CollectionConfiguration(new CollectionConfigurationInfo() { Metrics = metrics }, out errors, new ClockMock());
            var accumulatorManager = new QuickPulseDataAccumulatorManager(collectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            var instrumentationKey = "some ikey";
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = instrumentationKey });

            // ACT
            var dependencies = new[]
            {
                new DependencyTelemetry() { Id = "1", Success = true, Data = "500" },
                new DependencyTelemetry() { Id = "2", Success = false, Data = "500" },
                new DependencyTelemetry() { Id = "3", Success = true, Data = "501" },
                new DependencyTelemetry() { Id = "4", Success = false, Data = "501" },
                new DependencyTelemetry() { Id = "5", Success = true, Data = "499" },
                new DependencyTelemetry() { Id = "6", Success = false, Data = "499" },
                new DependencyTelemetry() { Id = "7", Success = true, Data = "201" },
                new DependencyTelemetry() { Id = "8", Success = false, Data = "201" },
                new DependencyTelemetry() { Id = "9", Success = true, Data = "blah" },
                new DependencyTelemetry() { Id = "10", Success = false, Data = "blah" },
            };

            Array.ForEach(dependencies, d => d.Context.InstrumentationKey = instrumentationKey);

            Array.ForEach(dependencies, telemetryProcessor.Process);

            // ASSERT
            Dictionary<string, AccumulatedValue> calculatedMetrics =
                accumulatorManager.CurrentDataAccumulator.CollectionConfigurationAccumulator.MetricAccumulators;

            Assert.AreEqual(2, calculatedMetrics.Count);

            Assert.AreEqual(
                "2, 4",
                string.Join(", ", calculatedMetrics["AverageIdOfFailedDependenciesGreaterThanOrEqualTo500"].Value.Reverse().ToArray()));
            Assert.AreEqual("7", string.Join(", ", calculatedMetrics["SumIdsOfSuccessfulDependenciesEqualTo201"].Value.Reverse().ToArray()));
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorCalculatesOperationalizedMetricsForExceptions()
        {
            // ARRANGE
            var filterInfoMessageGreaterThanOrEqualTo500 = new FilterInfo()
            {
                FieldName = "Message",
                Predicate = Predicate.GreaterThanOrEqual,
                Comparand = "500"
            };
            var filterInfoMessage200 = new FilterInfo() { FieldName = "Message", Predicate = Predicate.Equal, Comparand = "201" };
            var filterInfoSuccessful = new FilterInfo() { FieldName = "Sequence", Predicate = Predicate.Equal, Comparand = "true" };
            var filterInfoFailed = new FilterInfo() { FieldName = "Sequence", Predicate = Predicate.Equal, Comparand = "false" };

            var metrics = new[]
            {
                new OperationalizedMetricInfo()
                {
                    Id = "AverageIdOfFailedMessageGreaterThanOrEqualTo500",
                    TelemetryType = TelemetryType.Exception,
                    Projection = "Message",
                    Aggregation = AggregationType.Avg,
                    FilterGroups =
                        new[] { new FilterConjunctionGroupInfo() { Filters = new[] { filterInfoMessageGreaterThanOrEqualTo500, filterInfoFailed } } }
                },
                new OperationalizedMetricInfo()
                {
                    Id = "SumIdsOfSuccessfulMessageEqualTo201",
                    TelemetryType = TelemetryType.Exception,
                    Projection = "Message",
                    Aggregation = AggregationType.Sum,
                    FilterGroups = new[] { new FilterConjunctionGroupInfo() { Filters = new[] { filterInfoMessage200, filterInfoSuccessful } } }
                }
            };

            var collectionConfiguration = new CollectionConfiguration(new CollectionConfigurationInfo() { Metrics = metrics }, out errors, new ClockMock());
            var accumulatorManager = new QuickPulseDataAccumulatorManager(collectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            var instrumentationKey = "some ikey";
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = instrumentationKey });

            // ACT
            var exceptions = new[]
            {
                new ExceptionTelemetry() { Sequence = "true", Message = "500" }, new ExceptionTelemetry() { Sequence = "false", Message = "500" },
                new ExceptionTelemetry() { Sequence = "true", Message = "501" }, new ExceptionTelemetry() { Sequence = "false", Message = "501" },
                new ExceptionTelemetry() { Sequence = "true", Message = "499" }, new ExceptionTelemetry() { Sequence = "false", Message = "499" },
                new ExceptionTelemetry() { Sequence = "true", Message = "201" }, new ExceptionTelemetry() { Sequence = "false", Message = "201" },
                new ExceptionTelemetry() { Sequence = "true", Message = "blah" }, new ExceptionTelemetry() { Sequence = "false", Message = "blah" },
            };

            Array.ForEach(exceptions, e => e.Context.InstrumentationKey = instrumentationKey);

            Array.ForEach(exceptions, telemetryProcessor.Process);

            // ASSERT
            Dictionary<string, AccumulatedValue> calculatedMetrics =
                accumulatorManager.CurrentDataAccumulator.CollectionConfigurationAccumulator.MetricAccumulators;

            Assert.AreEqual(2, calculatedMetrics.Count);

            Assert.AreEqual(
                "500, 501",
                string.Join(", ", calculatedMetrics["AverageIdOfFailedMessageGreaterThanOrEqualTo500"].Value.Reverse().ToArray()));
            Assert.AreEqual("201", string.Join(", ", calculatedMetrics["SumIdsOfSuccessfulMessageEqualTo201"].Value.Reverse().ToArray()));
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorCalculatesOperationalizedMetricsForEvents()
        {
            // ARRANGE
            var filterInfoNameGreaterThanOrEqualTo500 = new FilterInfo()
            {
                FieldName = "Name",
                Predicate = Predicate.GreaterThanOrEqual,
                Comparand = "500"
            };
            var filterInfoResponseCode200 = new FilterInfo() { FieldName = "Name", Predicate = Predicate.Equal, Comparand = "201" };
            var filterInfoSuccessful = new FilterInfo() { FieldName = "Sequence", Predicate = Predicate.Equal, Comparand = "true" };
            var filterInfoFailed = new FilterInfo() { FieldName = "Sequence", Predicate = Predicate.Equal, Comparand = "false" };

            var metrics = new[]
            {
                new OperationalizedMetricInfo()
                {
                    Id = "AverageIdOfFailedEventsGreaterThanOrEqualTo500",
                    TelemetryType = TelemetryType.Event,
                    Projection = "Name",
                    Aggregation = AggregationType.Avg,
                    FilterGroups =
                        new[] { new FilterConjunctionGroupInfo() { Filters = new[] { filterInfoNameGreaterThanOrEqualTo500, filterInfoFailed } } }
                },
                new OperationalizedMetricInfo()
                {
                    Id = "SumIdsOfSuccessfulEventsEqualTo201",
                    TelemetryType = TelemetryType.Event,
                    Projection = "Name",
                    Aggregation = AggregationType.Sum,
                    FilterGroups = new[] { new FilterConjunctionGroupInfo() { Filters = new[] { filterInfoResponseCode200, filterInfoSuccessful } } }
                }
            };

            var collectionConfiguration = new CollectionConfiguration(new CollectionConfigurationInfo() { Metrics = metrics }, out errors, new ClockMock());
            var accumulatorManager = new QuickPulseDataAccumulatorManager(collectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            var instrumentationKey = "some ikey";
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = instrumentationKey });

            // ACT
            var events = new[]
            {
                new EventTelemetry() { Sequence = "true", Name = "500" }, new EventTelemetry() { Sequence = "false", Name = "500" },
                new EventTelemetry() { Sequence = "true", Name = "501" }, new EventTelemetry() { Sequence = "false", Name = "501" },
                new EventTelemetry() { Sequence = "true", Name = "499" }, new EventTelemetry() { Sequence = "false", Name = "499" },
                new EventTelemetry() { Sequence = "true", Name = "201" }, new EventTelemetry() { Sequence = "false", Name = "201" },
                new EventTelemetry() { Sequence = "true", Name = "blah" }, new EventTelemetry() { Sequence = "false", Name = "blah" },
            };

            Array.ForEach(events, e => e.Context.InstrumentationKey = instrumentationKey);

            Array.ForEach(events, telemetryProcessor.Process);

            // ASSERT
            Dictionary<string, AccumulatedValue> calculatedMetrics =
                accumulatorManager.CurrentDataAccumulator.CollectionConfigurationAccumulator.MetricAccumulators;

            Assert.AreEqual(2, calculatedMetrics.Count);

            Assert.AreEqual(
                "500, 501",
                string.Join(", ", calculatedMetrics["AverageIdOfFailedEventsGreaterThanOrEqualTo500"].Value.Reverse().ToArray()));
            Assert.AreEqual("201", string.Join(", ", calculatedMetrics["SumIdsOfSuccessfulEventsEqualTo201"].Value.Reverse().ToArray()));
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorCalculatesOperationalizedMetricsForTraces()
        {
            // ARRANGE
            var filterInfoNameGreaterThanOrEqualTo500 = new FilterInfo()
            {
                FieldName = "Message",
                Predicate = Predicate.GreaterThanOrEqual,
                Comparand = "500"
            };
            var filterInfoResponseCode200 = new FilterInfo() { FieldName = "Message", Predicate = Predicate.Equal, Comparand = "201" };
            var filterInfoSuccessful = new FilterInfo() { FieldName = "Sequence", Predicate = Predicate.Equal, Comparand = "true" };
            var filterInfoFailed = new FilterInfo() { FieldName = "Sequence", Predicate = Predicate.Equal, Comparand = "false" };

            var metrics = new[]
            {
                new OperationalizedMetricInfo()
                {
                    Id = "AverageIdOfFailedTracesGreaterThanOrEqualTo500",
                    TelemetryType = TelemetryType.Trace,
                    Projection = "Message",
                    Aggregation = AggregationType.Avg,
                    FilterGroups =
                        new[] { new FilterConjunctionGroupInfo() { Filters = new[] { filterInfoNameGreaterThanOrEqualTo500, filterInfoFailed } } }
                },
                new OperationalizedMetricInfo()
                {
                    Id = "SumIdsOfSuccessfulTracesEqualTo201",
                    TelemetryType = TelemetryType.Trace,
                    Projection = "Message",
                    Aggregation = AggregationType.Sum,
                    FilterGroups = new[] { new FilterConjunctionGroupInfo() { Filters = new[] { filterInfoResponseCode200, filterInfoSuccessful } } }
                }
            };

            var collectionConfiguration = new CollectionConfiguration(new CollectionConfigurationInfo() { Metrics = metrics }, out errors, new ClockMock());
            var accumulatorManager = new QuickPulseDataAccumulatorManager(collectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            var instrumentationKey = "some ikey";
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = instrumentationKey });

            // ACT
            var traces = new[]
            {
                new TraceTelemetry() { Sequence = "true", Message = "500" }, new TraceTelemetry() { Sequence = "false", Message = "500" },
                new TraceTelemetry() { Sequence = "true", Message = "501" }, new TraceTelemetry() { Sequence = "false", Message = "501" },
                new TraceTelemetry() { Sequence = "true", Message = "499" }, new TraceTelemetry() { Sequence = "false", Message = "499" },
                new TraceTelemetry() { Sequence = "true", Message = "201" }, new TraceTelemetry() { Sequence = "false", Message = "201" },
                new TraceTelemetry() { Sequence = "true", Message = "blah" }, new TraceTelemetry() { Sequence = "false", Message = "blah" },
            };

            Array.ForEach(traces, t => t.Context.InstrumentationKey = instrumentationKey);

            Array.ForEach(traces, telemetryProcessor.Process);

            // ASSERT
            Dictionary<string, AccumulatedValue> calculatedMetrics =
                accumulatorManager.CurrentDataAccumulator.CollectionConfigurationAccumulator.MetricAccumulators;

            Assert.AreEqual(2, calculatedMetrics.Count);

            Assert.AreEqual(
                "500, 501",
                string.Join(", ", calculatedMetrics["AverageIdOfFailedTracesGreaterThanOrEqualTo500"].Value.Reverse().ToArray()));
            Assert.AreEqual("201", string.Join(", ", calculatedMetrics["SumIdsOfSuccessfulTracesEqualTo201"].Value.Reverse().ToArray()));
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorOperationalizedMetricsIgnoresTelemetryWhereProjectionIsNotDouble()
        {
            // ARRANGE
            var metrics = new[]
            {
                new OperationalizedMetricInfo()
                {
                    Id = "Metric1",
                    TelemetryType = TelemetryType.Request,
                    Projection = "Id",
                    Aggregation = AggregationType.Avg,
                    FilterGroups = new FilterConjunctionGroupInfo[0]
                }
            };

            var collectionConfiguration = new CollectionConfiguration(new CollectionConfigurationInfo() { Metrics = metrics }, out errors, new ClockMock());
            var accumulatorManager = new QuickPulseDataAccumulatorManager(collectionConfiguration);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());
            var instrumentationKey = "some ikey";
            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = instrumentationKey });

            // ACT
            var requests = new[]
            {
                new RequestTelemetry() { Id = "1", Success = true, ResponseCode = "500" },
                new RequestTelemetry() { Id = "Not even a number...", Success = false, ResponseCode = "500" }
            };

            Array.ForEach(requests, r => r.Context.InstrumentationKey = instrumentationKey);

            Array.ForEach(requests, telemetryProcessor.Process);

            // ASSERT
            Dictionary<string, AccumulatedValue> calculatedMetrics =
                accumulatorManager.CurrentDataAccumulator.CollectionConfigurationAccumulator.MetricAccumulators;

            Assert.AreEqual(1, calculatedMetrics.Count);
            Assert.AreEqual(1.0d, calculatedMetrics["Metric1"].Value.Single());
        }

        [TestMethod]
        public void QuickPulseTelemetryProcessorHandlesOperationalizedMetricsInThreadSafeManner()
        {
            // ARRANGE
            var filterInfoAll200 = new FilterInfo() { FieldName = "ResponseCode", Predicate = Predicate.Equal, Comparand = "200" };
            var filterInfoAll500 = new FilterInfo() { FieldName = "ResponseCode", Predicate = Predicate.Equal, Comparand = "500" };
            var filterInfoAllSuccessful = new FilterInfo() { FieldName = "Success", Predicate = Predicate.Equal, Comparand = "true" };
            var filterInfoAllFailed = new FilterInfo() { FieldName = "Success", Predicate = Predicate.Equal, Comparand = "false" };
            var filterInfoAllFast = new FilterInfo() { FieldName = "Duration", Predicate = Predicate.LessThan, Comparand = "5000" };
            var filterInfoAllSlow = new FilterInfo() { FieldName = "Duration", Predicate = Predicate.GreaterThanOrEqual, Comparand = "5000" };

            var metrics1 = new[]
            {
                new OperationalizedMetricInfo()
                {
                    Id = "AllGood1",
                    TelemetryType = TelemetryType.Request,
                    Projection = "Id",
                    Aggregation = AggregationType.Avg,
                    FilterGroups = new[] { new FilterConjunctionGroupInfo() { Filters = new[] { filterInfoAll200, filterInfoAllSuccessful } } }
                },
                new OperationalizedMetricInfo()
                {
                    Id = "AllBad1",
                    TelemetryType = TelemetryType.Request,
                    Projection = "Id",
                    Aggregation = AggregationType.Avg,
                    FilterGroups = new[] { new FilterConjunctionGroupInfo() { Filters = new[] { filterInfoAll500, filterInfoAllFailed } } }
                },
                new OperationalizedMetricInfo()
                {
                    Id = "AllGoodFast1",
                    TelemetryType = TelemetryType.Request,
                    Projection = "Id",
                    Aggregation = AggregationType.Avg,
                    FilterGroups =
                        new[]
                        { new FilterConjunctionGroupInfo() { Filters = new[] { filterInfoAll200, filterInfoAllSuccessful, filterInfoAllFast } } }
                },
                new OperationalizedMetricInfo()
                {
                    Id = "AllBadSlow1",
                    TelemetryType = TelemetryType.Request,
                    Projection = "Id",
                    Aggregation = AggregationType.Avg,
                    FilterGroups =
                        new[] { new FilterConjunctionGroupInfo() { Filters = new[] { filterInfoAll500, filterInfoAllFailed, filterInfoAllSlow } } }
                }
            };

            var metrics2 = new[]
            {
                new OperationalizedMetricInfo()
                {
                    Id = "AllGood2",
                    TelemetryType = TelemetryType.Request,
                    Projection = "Id",
                    Aggregation = AggregationType.Avg,
                    FilterGroups = new[] { new FilterConjunctionGroupInfo() { Filters = new[] { filterInfoAll200, filterInfoAllSuccessful } } }
                },
                new OperationalizedMetricInfo()
                {
                    Id = "AllBad2",
                    TelemetryType = TelemetryType.Request,
                    Projection = "Id",
                    Aggregation = AggregationType.Avg,
                    FilterGroups = new[] { new FilterConjunctionGroupInfo() { Filters = new[] { filterInfoAll500, filterInfoAllFailed } } }
                },
                new OperationalizedMetricInfo()
                {
                    Id = "AllGoodFast2",
                    TelemetryType = TelemetryType.Request,
                    Projection = "Id",
                    Aggregation = AggregationType.Avg,
                    FilterGroups =
                        new[]
                        { new FilterConjunctionGroupInfo() { Filters = new[] { filterInfoAll200, filterInfoAllSuccessful, filterInfoAllFast } } }
                },
                new OperationalizedMetricInfo()
                {
                    Id = "AllBadSlow2",
                    TelemetryType = TelemetryType.Request,
                    Projection = "Id",
                    Aggregation = AggregationType.Avg,
                    FilterGroups =
                        new[] { new FilterConjunctionGroupInfo() { Filters = new[] { filterInfoAll500, filterInfoAllFailed, filterInfoAllSlow } } }
                }
            };

            var collectionConfiguration1 = new CollectionConfiguration(new CollectionConfigurationInfo() { Metrics = metrics1 }, out errors, new ClockMock());
            var collectionConfiguration2 = new CollectionConfiguration(new CollectionConfigurationInfo() { Metrics = metrics2 }, out errors, new ClockMock());

            var accumulatorManager = new QuickPulseDataAccumulatorManager(collectionConfiguration1);
            var telemetryProcessor = new QuickPulseTelemetryProcessor(new SimpleTelemetryProcessorSpy());

            ((IQuickPulseTelemetryProcessor)telemetryProcessor).StartCollection(
                accumulatorManager,
                new Uri("http://microsoft.com"),
                new TelemetryConfiguration() { InstrumentationKey = "some ikey" });

            int taskCount = 10000;
            int swapTaskCount = 100;
            var tasks = new List<Task>(taskCount);

            for (int i = 0; i < taskCount; i++)
            {
                var requestTelemetry = new RequestTelemetry()
                {
                    Id = i.ToString(),
                    ResponseCode = (i % 2 == 0) ? "200" : "500",
                    Success = i % 2 == 0,
                    Duration = TimeSpan.FromDays(i),
                    Context = { InstrumentationKey = "some ikey" }
                };

                var task = new Task(() => telemetryProcessor.Process(requestTelemetry));
                tasks.Add(task);
            }

            // shuffle in a bunch of accumulator swapping operations
            var accumulators = new List<QuickPulseDataAccumulator>();
            for (int i = 0; i < swapTaskCount; i++)
            {
                int localI = i;
                var swapTask = new Task(
                    () =>
                        {
                            lock (accumulators)
                            {
                                // switch the configuration when about half-way in
                                accumulators.Add(
                                    accumulatorManager.CompleteCurrentDataAccumulator(
                                        localI < swapTaskCount / 2 ? collectionConfiguration1 : collectionConfiguration2));
                            }
                        });

                tasks.Insert((int)((double)taskCount / swapTaskCount * i), swapTask);
            }

            // ACT
            tasks.ForEach(task => task.Start());

            Task.WaitAll(tasks.ToArray());

            // swap the last accumulator
            accumulators.Add(accumulatorManager.CompleteCurrentDataAccumulator(null));

            // ASSERT
            // validate that all accumulators add up to the correct totals
            var allGood1 = new List<double>();
            var allBad1 = new List<double>();
            var allGoodFast1 = new List<double>();
            var allBadSlow1 = new List<double>();
            var allGood2 = new List<double>();
            var allBad2 = new List<double>();
            var allGoodFast2 = new List<double>();
            var allBadSlow2 = new List<double>();
            foreach (var accumulator in accumulators)
            {
                Dictionary<string, AccumulatedValue> metricsValues = accumulator.CollectionConfigurationAccumulator.MetricAccumulators;

                try
                {
                    // configuration 1
                    allGood1.AddRange(metricsValues["AllGood1"].Value.Reverse().ToArray());
                    allBad1.AddRange(metricsValues["AllBad1"].Value.Reverse().ToArray());
                    allGoodFast1.AddRange(metricsValues["AllGoodFast1"].Value.Reverse().ToArray());
                    allBadSlow1.AddRange(metricsValues["AllBadSlow1"].Value.Reverse().ToArray());
                }
                catch
                {
                    // metrics not found, wrong configuration
                }

                try
                {
                    // configuration 2
                    allGood2.AddRange(metricsValues["AllGood2"].Value.Reverse().ToArray());
                    allBad2.AddRange(metricsValues["AllBad2"].Value.Reverse().ToArray());
                    allGoodFast2.AddRange(metricsValues["AllGoodFast2"].Value.Reverse().ToArray());
                    allBadSlow2.AddRange(metricsValues["AllBadSlow2"].Value.Reverse().ToArray());
                }
                catch
                {
                    // metrics not found, wrong configuration
                }
            }

            Assert.AreEqual(taskCount / 2, allGood1.Count + allGood2.Count);
            Assert.IsTrue(allGood1.All(value => (int)value % 2 == 0));
            Assert.IsTrue(allGood2.All(value => (int)value % 2 == 0));

            Assert.AreEqual(taskCount / 2, allBad1.Count + allBad2.Count);
            Assert.IsTrue(allBad1.All(value => (int)value % 2 == 1));
            Assert.IsTrue(allBad2.All(value => (int)value % 2 == 1));

            Assert.AreEqual(taskCount / 4, allGoodFast1.Count + allGoodFast2.Count);
            Assert.IsTrue(allGoodFast1.All(value => (int)value % 2 == 0 && value < taskCount / 2));
            Assert.IsTrue(allGoodFast2.All(value => (int)value % 2 == 0 && value < taskCount / 2));
            Assert.IsTrue(allGoodFast1.Count > allGoodFast2.Count);

            Assert.AreEqual(taskCount / 4, allBadSlow1.Count + allBadSlow2.Count);
            Assert.IsTrue(allBadSlow1.All(value => (int)value % 2 == 1 && value >= taskCount / 2));
            Assert.IsTrue(allBadSlow2.All(value => (int)value % 2 == 1 && value >= taskCount / 2));
            Assert.IsTrue(allBadSlow1.Count < allBadSlow2.Count);
        }
    }
}