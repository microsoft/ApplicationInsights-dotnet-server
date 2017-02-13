namespace Unit.Tests
{
    using Microsoft.ApplicationInsights.Extensibility.Filtering;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Filter tests.
    /// </summary>
    [TestClass]
    public class FilterInfoTests
    {
        [TestMethod]
        public void FilterInfoEqualityAccountsForFieldNameTest()
        {
            // ARRANGE
            var filterInfo1 = new FilterInfo() { FieldName = "FieldName", Predicate = Predicate.Equal, Comparand = "Comparand" };
            var filterInfo2 = new FilterInfo() { FieldName = "FieldName", Predicate = Predicate.Equal, Comparand = "Comparand" };
            var filterInfo3 = new FilterInfo() { FieldName = "FieldName1", Predicate = Predicate.Equal, Comparand = "Comparand" };

            // ACT
            bool equalityCheck = filterInfo1.Equals(filterInfo2) && filterInfo1 == filterInfo2 && filterInfo1.ToString() == filterInfo2.ToString();
            bool inequalityCheck = !filterInfo1.Equals(filterInfo3) && !(filterInfo1 == filterInfo3) && filterInfo1.ToString() != filterInfo3.ToString();
            
            // ASSERT
            Assert.IsTrue(equalityCheck);
            Assert.IsTrue(inequalityCheck);
        }

        [TestMethod]
        public void FilterInfoEqualityAccountsForPredicateTest()
        {
            // ARRANGE
            var filterInfo1 = new FilterInfo() { FieldName = "FieldName", Predicate = Predicate.Equal, Comparand = "Comparand" };
            var filterInfo2 = new FilterInfo() { FieldName = "FieldName", Predicate = Predicate.Equal, Comparand = "Comparand" };
            var filterInfo3 = new FilterInfo() { FieldName = "FieldName", Predicate = Predicate.NotEqual, Comparand = "Comparand" };

            // ACT
            bool equalityCheck = filterInfo1.Equals(filterInfo2) && filterInfo1 == filterInfo2 && filterInfo1.ToString() == filterInfo2.ToString();
            bool inequalityCheck = !filterInfo1.Equals(filterInfo3) && !(filterInfo1 == filterInfo3) && filterInfo1.ToString() != filterInfo3.ToString();

            // ASSERT
            Assert.IsTrue(equalityCheck);
            Assert.IsTrue(inequalityCheck);
        }

        [TestMethod]
        public void FilterInfoEqualityAccountsForComparandTest()
        {
            // ARRANGE
            var filterInfo1 = new FilterInfo() { FieldName = "FieldName", Predicate = Predicate.Equal, Comparand = "Comparand" };
            var filterInfo2 = new FilterInfo() { FieldName = "FieldName", Predicate = Predicate.Equal, Comparand = "Comparand" };
            var filterInfo3 = new FilterInfo() { FieldName = "FieldName", Predicate = Predicate.Equal, Comparand = "Comparand1" };

            // ACT
            bool equalityCheck = filterInfo1.Equals(filterInfo2) && filterInfo1 == filterInfo2 && filterInfo1.ToString() == filterInfo2.ToString();
            bool inequalityCheck = !filterInfo1.Equals(filterInfo3) && !(filterInfo1 == filterInfo3) && filterInfo1.ToString() != filterInfo3.ToString();

            // ASSERT
            Assert.IsTrue(equalityCheck);
            Assert.IsTrue(inequalityCheck);
        }
    }
}