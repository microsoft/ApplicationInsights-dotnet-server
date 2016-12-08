namespace Unit.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading;

    using Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.Implementation.QuickPulse;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class QuickPulseTopCpuCollectorTests
    {
        [TestInitialize]
        public void TestInitialize()
        {
        }

        [TestMethod]
        public void QuickPulseTopCpuCollectorReturnsNothingWhenCalledForTheFirstTime()
        {
            // ARRANGE
            var processProvider = new QuickPulseProcessProviderMock();
            processProvider.Processes = new List<QuickPulseProcess>()
                                            {
                                                new QuickPulseProcess("Process1", TimeSpan.FromSeconds(50)),
                                                new QuickPulseProcess("Process2", TimeSpan.FromSeconds(100)),
                                                new QuickPulseProcess("Process3", TimeSpan.FromSeconds(75)),
                                                new QuickPulseProcess("Process4", TimeSpan.FromSeconds(25)),
                                                new QuickPulseProcess("Process5", TimeSpan.FromSeconds(125)),
                                            };
            var timeProvider = new ClockMock();
            var collector = new QuickPulseTopCpuCollector(timeProvider, processProvider);

            // ACT
            var topProcesses = collector.GetTopProcessesByCpu(3).ToList();

            // ASSERT
            Assert.AreEqual(0, topProcesses.Count);
        }

        [TestMethod]
        public void QuickPulseTopCpuCollectorReturnsTopProcessesByCpuWhenTotalTimeIsUnavailable()
        {
            // ARRANGE
            TimeSpan interval = TimeSpan.FromSeconds(2);
            var processProvider = new QuickPulseProcessProviderMock() { OverallTimeValue = null };
            var baseProcessorTime = TimeSpan.FromSeconds(100);
            processProvider.Processes = new List<QuickPulseProcess>()
                                            {
                                                new QuickPulseProcess("Process1", baseProcessorTime),
                                                new QuickPulseProcess("Process2", baseProcessorTime),
                                                new QuickPulseProcess("Process3", baseProcessorTime),
                                                new QuickPulseProcess("Process4", baseProcessorTime),
                                                new QuickPulseProcess("Process5", baseProcessorTime),
                                            };
            var timeProvider = new ClockMock();
            var collector = new QuickPulseTopCpuCollector(timeProvider, processProvider);

            // ACT
            collector.GetTopProcessesByCpu(3);

            timeProvider.FastForward(interval);

            processProvider.Processes = new List<QuickPulseProcess>()
                                            {
                                                new QuickPulseProcess("Process1", baseProcessorTime + TimeSpan.FromMilliseconds(50)),
                                                new QuickPulseProcess("Process2", baseProcessorTime + TimeSpan.FromMilliseconds(100)),
                                                new QuickPulseProcess("Process3", baseProcessorTime + TimeSpan.FromMilliseconds(75)),
                                                new QuickPulseProcess("Process4", baseProcessorTime + TimeSpan.FromMilliseconds(25)),
                                                new QuickPulseProcess("Process5", baseProcessorTime + TimeSpan.FromMilliseconds(125)),
                                            };

            var topProcesses = collector.GetTopProcessesByCpu(3).ToList();

            // ASSERT
            Assert.AreEqual(3, topProcesses.Count);

            Assert.AreEqual("Process5", topProcesses[0].Item1);
            Assert.AreEqual((int)((125.0 / (Environment.ProcessorCount * interval.TotalMilliseconds)) * 100), topProcesses[0].Item2);

            Assert.AreEqual("Process2", topProcesses[1].Item1);
            Assert.AreEqual((int)((100.0 / (Environment.ProcessorCount * interval.TotalMilliseconds)) * 100), topProcesses[1].Item2);

            Assert.AreEqual("Process3", topProcesses[2].Item1);
            Assert.AreEqual((int)((75.0 / (Environment.ProcessorCount * interval.TotalMilliseconds)) * 100), topProcesses[2].Item2);
        }

        [TestMethod]
        public void QuickPulseTopCpuCollectorReturnsTopProcessesByCpuWhenTotalTimeIsAvailable()
        {
            // ARRANGE
            TimeSpan interval = TimeSpan.FromSeconds(2);
            var processProvider = new QuickPulseProcessProviderMock();
            var baseProcessorTime = TimeSpan.FromSeconds(100);
            processProvider.Processes = new List<QuickPulseProcess>()
                                            {
                                                new QuickPulseProcess("Process1", baseProcessorTime),
                                                new QuickPulseProcess("Process2", baseProcessorTime),
                                                new QuickPulseProcess("Process3", baseProcessorTime),
                                                new QuickPulseProcess("Process4", baseProcessorTime),
                                                new QuickPulseProcess("Process5", baseProcessorTime)
                                            };
            var timeProvider = new ClockMock();
            var collector = new QuickPulseTopCpuCollector(timeProvider, processProvider);

            // ACT

            // doesn't matter, some large value
            processProvider.OverallTimeValue = TimeSpan.FromTicks(1000 * baseProcessorTime.Ticks);
            collector.GetTopProcessesByCpu(3);

            timeProvider.FastForward(interval);

            processProvider.Processes = new List<QuickPulseProcess>()
                                            {
                                                new QuickPulseProcess("Process1", baseProcessorTime + TimeSpan.FromMilliseconds(50)),
                                                new QuickPulseProcess("Process2", baseProcessorTime + TimeSpan.FromMilliseconds(100)),
                                                new QuickPulseProcess("Process3", baseProcessorTime + TimeSpan.FromMilliseconds(75)),
                                                new QuickPulseProcess("Process4", baseProcessorTime + TimeSpan.FromMilliseconds(25)),
                                                new QuickPulseProcess("Process5", baseProcessorTime + TimeSpan.FromMilliseconds(125))
                                            };
            processProvider.OverallTimeValue += TimeSpan.FromTicks(Environment.ProcessorCount * interval.Ticks);

            var topProcesses = collector.GetTopProcessesByCpu(3).ToList();

            // ASSERT
            Assert.AreEqual(3, topProcesses.Count);

            Assert.AreEqual("Process5", topProcesses[0].Item1);
            Assert.AreEqual((int)((125.0 / (Environment.ProcessorCount * interval.TotalMilliseconds)) * 100), topProcesses[0].Item2);

            Assert.AreEqual("Process2", topProcesses[1].Item1);
            Assert.AreEqual((int)((100.0 / (Environment.ProcessorCount * interval.TotalMilliseconds)) * 100), topProcesses[1].Item2);

            Assert.AreEqual("Process3", topProcesses[2].Item1);
            Assert.AreEqual((int)((75.0 / (Environment.ProcessorCount * interval.TotalMilliseconds)) * 100), topProcesses[2].Item2);
        }
        
        [TestMethod]
        public void QuickPulseTopCpuCollectorHandlesExceptionFromProcessProvider()
        {
            // ARRANGE
            var processProvider = new QuickPulseProcessProviderMock() { AlwaysThrow = true };
            var timeProvider = new ClockMock();
            var collector = new QuickPulseTopCpuCollector(timeProvider, processProvider);

            // ACT
            var topProcesses = collector.GetTopProcessesByCpu(3);
            
            // ASSERT
            Assert.AreEqual(0, topProcesses.Count());
        }

        [TestMethod]
        public void QuickPulseTopCpuCollectorCleansUpStateWhenProcessesGoAway()
        {
            // ARRANGE
            var processProvider = new QuickPulseProcessProviderMock();
            var baseProcessorTime = TimeSpan.FromSeconds(100);
            processProvider.Processes = new List<QuickPulseProcess>()
                                            {
                                                new QuickPulseProcess("Process1", baseProcessorTime),
                                                new QuickPulseProcess("Process2", baseProcessorTime),
                                                new QuickPulseProcess("Process3", baseProcessorTime),
                                                new QuickPulseProcess("Process4", baseProcessorTime),
                                                new QuickPulseProcess("Process5", baseProcessorTime),
                                            };
            var timeProvider = new ClockMock();
            var collector = new QuickPulseTopCpuCollector(timeProvider, processProvider);

            var processDictionary =
                QuickPulseTestHelper.GetPrivateField(collector, "processObservations") as Dictionary<string, TimeSpan>;

            // ACT
            collector.GetTopProcessesByCpu(3);
            int itemCount1 = processDictionary.Count;

            timeProvider.FastForward(TimeSpan.FromSeconds(1));

            processProvider.Processes = new List<QuickPulseProcess>() { new QuickPulseProcess("Process1", baseProcessorTime) };
            collector.GetTopProcessesByCpu(3);
            int itemCount3 = processDictionary.Count;

            // ASSERT
            Assert.AreEqual(5, itemCount1);
            Assert.AreEqual(1, itemCount3);
        }
    }
}