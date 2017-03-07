namespace Unit.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.ExceptionServices;

    using Microsoft.ApplicationInsights.Extensibility.Filtering;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class FilterConjunctionGroupTests
    {
        [TestMethod]
        public void FilterConjunctionGroupPassesWhenAllFiltersPass()
        {
            // ARRANGE
            string[] errors;
            var filterGroupInfo = new FilterConjunctionGroupInfo()
                                      {
                                          Filters =
                                              new[]
                                                  {
                                                      new FilterInfo()
                                                          {
                                                              FieldName = "StringField",
                                                              Predicate = Predicate.Contains,
                                                              Comparand = "apple"
                                                          },
                                                      new FilterInfo()
                                                          {
                                                              FieldName = "StringField",
                                                              Predicate = Predicate.Contains,
                                                              Comparand = "dog"
                                                          },
                                                      new FilterInfo()
                                                          {
                                                              FieldName = "StringField",
                                                              Predicate = Predicate.Contains,
                                                              Comparand = "red"
                                                          }
                                                  }
                                      };
            var filterGroup = new FilterConjunctionGroup<TelemetryMock>(filterGroupInfo, out errors);
            var telemetry = new TelemetryMock() { StringField = "This string contains all valuable words: 'apple', 'dog', and 'red'." };

            // ACT
            string[] runtimeErrors;
            bool filtersPassed = filterGroup.CheckFilters(telemetry, out runtimeErrors);

            // ASSERT
            Assert.IsTrue(filtersPassed);
            Assert.AreEqual(0, errors.Length);
            Assert.AreEqual(0, runtimeErrors.Length);
        }

        [TestMethod]
        public void FilterConjunctionGroupFailsWhenOneFilterFails()
        {
            // ARRANGE
            string[] errors;
            var filterGroupInfo = new FilterConjunctionGroupInfo()
                                      {
                                          Filters =
                                              new[]
                                                  {
                                                      new FilterInfo()
                                                          {
                                                              FieldName = "StringField",
                                                              Predicate = Predicate.Contains,
                                                              Comparand = "apple"
                                                          },
                                                      new FilterInfo()
                                                          {
                                                              FieldName = "StringField",
                                                              Predicate = Predicate.Contains,
                                                              Comparand = "dog"
                                                          },
                                                      new FilterInfo()
                                                          {
                                                              FieldName = "StringField",
                                                              Predicate = Predicate.Contains,
                                                              Comparand = "red"
                                                          }
                                                  }
                                      };
            var filterGroup = new FilterConjunctionGroup<TelemetryMock>(filterGroupInfo, out errors);
            var telemetry = new TelemetryMock()
                                {
                                    StringField =
                                        "This string contains some of the valuable words: 'apple', 'red', but doesn't mention the man's best friend..."
                                };

            // ACT
            string[] runtimeErrors;
            bool filtersPassed = filterGroup.CheckFilters(telemetry, out runtimeErrors);

            // ASSERT
            Assert.IsFalse(filtersPassed);
            Assert.AreEqual(0, errors.Length);
            Assert.AreEqual(0, runtimeErrors.Length);
        }

        [TestMethod]
        public void FilterConjunctionGroupPassesWhenNoFilters()
        {
            // ARRANGE
            string[] errors;
            var filterGroupInfo = new FilterConjunctionGroupInfo() { Filters = new FilterInfo[0] };
            var filterGroup = new FilterConjunctionGroup<TelemetryMock>(filterGroupInfo, out errors);
            var telemetry = new TelemetryMock();

            // ACT
            string[] runtimeErrors;
            bool filtersPassed = filterGroup.CheckFilters(telemetry, out runtimeErrors);

            // ASSERT
            Assert.IsTrue(filtersPassed);
            Assert.AreEqual(0, errors.Length);
            Assert.AreEqual(0, runtimeErrors.Length);
        }

        [TestMethod]
        public void FilterConjunctionGroupReportsErrorsWhenIncorrectFiltersArePresent()
        {
            // ARRANGE
            string[] errors;
            var filterGroupInfo = new FilterConjunctionGroupInfo()
                                      {
                                          Filters =
                                              new[]
                                                  {
                                                      new FilterInfo()
                                                          {
                                                              FieldName = "NonExistentField",
                                                              Predicate = Predicate.Contains,
                                                              Comparand = "apple"
                                                          },
                                                      new FilterInfo()
                                                          {
                                                              FieldName = "BooleanField",
                                                              Predicate = Predicate.Contains,
                                                              Comparand = "dog"
                                                          },
                                                      new FilterInfo()
                                                          {
                                                              FieldName = "StringField",
                                                              Predicate = Predicate.Contains,
                                                              Comparand = "red"
                                                          }
                                                  }
                                      };
            var filterGroup = new FilterConjunctionGroup<TelemetryMock>(filterGroupInfo, out errors);
            var telemetry = new TelemetryMock() { StringField = "red" };

            // ACT
            string[] runtimeErrors;
            bool filtersPassed = filterGroup.CheckFilters(telemetry, out runtimeErrors);

            // ASSERT
            Assert.IsTrue(filtersPassed);

            Assert.AreEqual(2, errors.Length);
            Assert.IsTrue(errors[0].Contains("NonExistentField"));
            Assert.IsTrue(errors[1].Contains("dog"));

            Assert.AreEqual(0, runtimeErrors.Length);
        }
    }
}