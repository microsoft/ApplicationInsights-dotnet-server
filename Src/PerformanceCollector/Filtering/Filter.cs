namespace Microsoft.ApplicationInsights.Extensibility.Filtering
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Reflection;

    using Microsoft.ApplicationInsights.DataContracts;

    /// <summary>
    /// Filter determines whether a telemetry document matches the criterion.
    /// </summary>
    /// <typeparam name="TTelemetry">Type of telemetry documents.</typeparam>
    internal class Filter<TTelemetry>
    {
        private const string FieldNameCustomDimensionsPrefix = "CustomDimensions.";

        private const string FieldNameCustomMetricsPrefix = "CustomMetrics.";

        private const string FieldNameAsterisk = "*";

        private const string CustomMetricsPropertyName = "Metrics";

        private const string CustomDimensionsPropertyName = "Properties";

        private static readonly MethodInfo DoubleToStringMethodInfo = GetMethodInfo<double, string>(x => x.ToString(CultureInfo.InvariantCulture));

        private static readonly MethodInfo UriToStringMethodInfo = GetMethodInfo<Uri, string>(x => x.ToString());

        private static readonly MethodInfo StringIndexOfMethodInfo =
            GetMethodInfo<string, string, int>((x, y) => x.IndexOf(y, StringComparison.OrdinalIgnoreCase));

        private static readonly MethodInfo StringEqualsMethodInfo =
            GetMethodInfo<string, string, bool>((x, y) => x.Equals(y, StringComparison.OrdinalIgnoreCase));

        private static readonly MethodInfo DoubleParseMethodInfo = typeof(double).GetMethod(
            "Parse",
            new[] { typeof(string), typeof(IFormatProvider) });

        private static readonly MethodInfo DictionaryStringStringTryGetValueMethodInfo = typeof(IDictionary<string, string>).GetMethod("TryGetValue");

        private static readonly MethodInfo DictionaryStringDoubleTryGetValueMethodInfo = typeof(IDictionary<string, double>).GetMethod("TryGetValue");

        private static readonly MethodInfo DictionaryStringStringScanMethodInfo =
            GetMethodInfo<IDictionary<string, string>, string, bool>((dict, searchValue) => Filter<int>.ScanDictionary(dict, searchValue));

        private static readonly MethodInfo DictionaryStringDoubleScanMethodInfo =
           GetMethodInfo<IDictionary<string, double>, string, bool>((dict, searchValue) => Filter<int>.ScanDictionary(dict, searchValue));

        private readonly Func<TTelemetry, bool> filterLambda;

        private readonly double? comparandDouble;

        private readonly bool? comparandBoolean;

        private readonly TimeSpan? comparandTimeSpan;

        private readonly string comparand;

        private readonly Predicate predicate;

        private readonly string fieldName;

        private readonly FilterInfo info;

        public Filter(FilterInfo filterInfo)
        {
            ValidateInput(filterInfo);

            this.info = filterInfo;

            this.fieldName = filterInfo.FieldName;
            this.predicate = filterInfo.Predicate;
            this.comparand = filterInfo.Comparand;

            FieldNameType fieldNameType;
            Type fieldType = GetFieldType(filterInfo.FieldName, out fieldNameType);
            this.ThrowOnInvalidFilter(
                null,
                fieldNameType == FieldNameType.AnyField && this.predicate != Predicate.Contains && this.predicate != Predicate.DoesNotContain);

            double comparandDouble;
            this.comparandDouble = double.TryParse(filterInfo.Comparand, out comparandDouble) ? comparandDouble : (double?)null;

            bool comparandBoolean;
            this.comparandBoolean = bool.TryParse(filterInfo.Comparand, out comparandBoolean) ? comparandBoolean : (bool?)null;

            TimeSpan comparandTimeSpan;
            this.comparandTimeSpan = TimeSpan.TryParse(filterInfo.Comparand, CultureInfo.InvariantCulture, out comparandTimeSpan)
                                         ? comparandTimeSpan
                                         : (TimeSpan?)null;

            ParameterExpression documentExpression = Expression.Variable(typeof(TTelemetry));

            Expression comparisonExpression;

            try
            {
                if (fieldNameType == FieldNameType.AnyField)
                {
                    // multiple fields => multiple comparison expressions connected with ORs
                    comparisonExpression = this.ProduceComparatorExpression(documentExpression);
                }
                else
                {
                    // a single field filterInfo.FieldName of type fieldType => a single comparison expression
                    Expression fieldExpression = ProduceFieldExpression(documentExpression, filterInfo.FieldName, fieldNameType);

                    comparisonExpression = this.ProduceComparatorExpression(fieldExpression, fieldType);
                }
            }
            catch (Exception e)
            {
                throw new ArgumentOutOfRangeException(string.Format(CultureInfo.InvariantCulture, "Could not construct the filter."), e);
            }

            try
            {
                Expression<Func<TTelemetry, bool>> lambdaExpression = Expression.Lambda<Func<TTelemetry, bool>>(
                    comparisonExpression,
                    documentExpression);

                this.filterLambda = lambdaExpression.Compile();
            }
            catch (Exception e)
            {
                throw new ArgumentOutOfRangeException(string.Format(CultureInfo.InvariantCulture, "Could not compile the filter."), e);
            }
        }
        
        public bool Check(TTelemetry document)
        {
            try
            {
                return this.filterLambda(document);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Runtime error while filtering."), e);
            }
        }

        public override string ToString()
        {
            return this.info?.ToString() ?? string.Empty;
        }

        internal static Expression ProduceFieldExpression(ParameterExpression documentExpression, string fieldName, FieldNameType fieldNameType)
        {
            switch (fieldNameType)
            {
                case FieldNameType.FieldName:
                    return Expression.Property(documentExpression, fieldName);
                case FieldNameType.CustomMetricName:
                    string customMetricName = fieldName.Substring(
                        FieldNameCustomMetricsPrefix.Length,
                        fieldName.Length - FieldNameCustomMetricsPrefix.Length);

                    return CreateDictionaryAccessExpression(
                        documentExpression,
                        CustomMetricsPropertyName,
                        DictionaryStringDoubleTryGetValueMethodInfo,
                        typeof(double),
                        customMetricName);
                case FieldNameType.CustomDimensionName:
                    string customDimensionName = fieldName.Substring(
                        FieldNameCustomDimensionsPrefix.Length,
                        fieldName.Length - FieldNameCustomDimensionsPrefix.Length);

                    return CreateDictionaryAccessExpression(
                        documentExpression,
                        CustomDimensionsPropertyName,
                        DictionaryStringStringTryGetValueMethodInfo,
                        typeof(string),
                        customDimensionName);
                default:
                    throw new ArgumentOutOfRangeException(nameof(fieldNameType), fieldNameType, null);
            }
        }

        internal static Type GetFieldType(string fieldName, out FieldNameType fieldNameType)
        {
            if (fieldName.StartsWith(FieldNameCustomDimensionsPrefix, StringComparison.Ordinal))
            {
                fieldNameType = FieldNameType.CustomDimensionName;
                return typeof(string);
            }

            if (fieldName.StartsWith(FieldNameCustomMetricsPrefix, StringComparison.Ordinal))
            {
                fieldNameType = FieldNameType.CustomMetricName;
                return typeof(double);
            }

            if (fieldName.StartsWith(FieldNameAsterisk, StringComparison.Ordinal))
            {
                fieldNameType = FieldNameType.AnyField;
                return null;
            }

            // no special case in filterInfo.FieldName, treat it as the name of a property in TTelemetry type
            fieldNameType = FieldNameType.FieldName;
            return GetPropertyTypeFromName(fieldName);
        }

        private static Expression CreateDictionaryAccessExpression(ParameterExpression documentExpression, string dictionaryName, MethodInfo tryGetValueMethodInfo, Type valueType, string keyValue)
        {
            // valueType value;
            // document.dictionaryName.TryGetValue(keyValue, out value)
            // return value;
            ParameterExpression valueVariable = Expression.Variable(valueType);

            MemberExpression properties = Expression.Property(documentExpression, dictionaryName);
            MethodCallExpression tryGetValueCall = Expression.Call(properties, tryGetValueMethodInfo, Expression.Constant(keyValue), valueVariable);

            // a block will "return" its last expression
            return Expression.Block(valueType, new[] { valueVariable }, tryGetValueCall, valueVariable);
        }
        
        private static MethodInfo GetMethodInfo<T, TResult>(Expression<Func<T, TResult>> expression)
        {
            var member = expression.Body as MethodCallExpression;

            if (member != null)
            {
                return member.Method;
            }

            throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Expression is not a method"), nameof(expression));
        }

        private static MethodInfo GetMethodInfo<T1, T2, TResult>(Expression<Func<T1, T2, TResult>> expression)
        {
            var member = expression.Body as MethodCallExpression;

            if (member != null)
            {
                return member.Method;
            }

            throw new ArgumentException(string.Format(CultureInfo.InvariantCulture, "Expression is not a method"), nameof(expression));
        }

        private static Type GetPropertyTypeFromName(string propertyName)
        {
            PropertyInfo fieldPropertyInfo;
            try
            {
                fieldPropertyInfo = typeof(TTelemetry).GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            }
            catch (Exception e)
            {
                throw new ArgumentOutOfRangeException(string.Format(CultureInfo.InvariantCulture, "Error finding property {0} in the type {1}", propertyName, typeof(TTelemetry).FullName), e);
            }

            if (fieldPropertyInfo == null)
            {
                throw new ArgumentOutOfRangeException(nameof(propertyName), string.Format(CultureInfo.InvariantCulture, "Could not find the property {0} in the type {1}", propertyName, typeof(TTelemetry).FullName));
            }

            return fieldPropertyInfo.PropertyType;
        }

        private static void ValidateInput(FilterInfo filterInfo)
        {
            if (filterInfo == null)
            {
                throw new ArgumentNullException(nameof(filterInfo));
            }

            if (string.IsNullOrEmpty(filterInfo.FieldName))
            {
                throw new ArgumentNullException(nameof(filterInfo.FieldName), string.Format(CultureInfo.InvariantCulture, "Parameter must be specified."));
            }

            if (filterInfo.Comparand == null)
            {
                throw new ArgumentNullException(nameof(filterInfo.Comparand), string.Format(CultureInfo.InvariantCulture, "Parameter cannot be null."));
            }
        }

        private static bool ScanDictionary(IDictionary<string, string> dict, string searchValue)
        {
            return dict?.Values.Any(val => (val ?? string.Empty).IndexOf(searchValue ?? string.Empty, StringComparison.OrdinalIgnoreCase) != -1)
                   ?? false;
        }

        private static bool ScanDictionary(IDictionary<string, double> dict, string searchValue)
        {
            return dict?.Values.Any(val => val.ToString(CultureInfo.InvariantCulture).IndexOf(searchValue, StringComparison.OrdinalIgnoreCase) != -1)
                   ?? false;
        }

        private Expression ProduceComparatorExpression(Expression fieldExpression, Type fieldType)
        {
            // this must determine an appropriate runtime comparison given the field type, the predicate, and the comparand
            TypeCode fieldTypeCode = Type.GetTypeCode(fieldType);
            switch (fieldTypeCode)
            {
                case TypeCode.Boolean:
                    {
                        this.ThrowOnInvalidFilter(fieldType, !this.comparandBoolean.HasValue);

                        switch (this.predicate)
                        {
                            case Predicate.Equal:
                                Func<bool, bool> comparator = fieldValue => fieldValue == this.comparandBoolean.Value;
                                return Expression.Call(Expression.Constant(comparator.Target), comparator.Method, fieldExpression);
                            case Predicate.NotEqual:
                                comparator = fieldValue => fieldValue != this.comparandBoolean.Value;
                                return Expression.Call(Expression.Constant(comparator.Target), comparator.Method, fieldExpression);
                            default:
                                this.ThrowOnInvalidFilter(fieldType);
                                break;
                        }
                    }

                    break;
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                    {
                        // in order for the expression to compile, we must cast to double unless it's already double
                        // we're using double as the common lowest denominator for all numerical types
                        Expression fieldConvertedExpression = fieldTypeCode == TypeCode.Double ? fieldExpression : Expression.ConvertChecked(fieldExpression, typeof(double));

                        switch (this.predicate)
                        {
                            case Predicate.Equal:
                                this.ThrowOnInvalidFilter(fieldType, !this.comparandDouble.HasValue);
                                return Expression.Equal(fieldConvertedExpression, Expression.Constant(this.comparandDouble.Value));
                            case Predicate.NotEqual:
                                this.ThrowOnInvalidFilter(fieldType, !this.comparandDouble.HasValue);
                                return Expression.NotEqual(fieldConvertedExpression, Expression.Constant(this.comparandDouble.Value));
                            case Predicate.LessThan:
                                this.ThrowOnInvalidFilter(fieldType, !this.comparandDouble.HasValue);
                                return Expression.LessThan(fieldConvertedExpression, Expression.Constant(this.comparandDouble.Value));
                            case Predicate.GreaterThan:
                                this.ThrowOnInvalidFilter(fieldType, !this.comparandDouble.HasValue);
                                return Expression.GreaterThan(fieldConvertedExpression, Expression.Constant(this.comparandDouble.Value));
                            case Predicate.LessThanOrEqual:
                                this.ThrowOnInvalidFilter(fieldType, !this.comparandDouble.HasValue);
                                return Expression.LessThanOrEqual(fieldConvertedExpression, Expression.Constant(this.comparandDouble.Value));
                            case Predicate.GreaterThanOrEqual:
                                this.ThrowOnInvalidFilter(fieldType, !this.comparandDouble.HasValue);
                                return Expression.GreaterThanOrEqual(fieldConvertedExpression, Expression.Constant(this.comparandDouble.Value));
                            case Predicate.Contains:
                                // fieldValue.ToString(CultureInfo.InvariantCulture).IndexOf(this.comparand, StringComparison.OrdinalIgnoreCase) != -1
                                Expression toStringCall = Expression.Call(fieldConvertedExpression, DoubleToStringMethodInfo, Expression.Constant(CultureInfo.InvariantCulture));
                                Expression indexOfCall = Expression.Call(toStringCall, StringIndexOfMethodInfo, Expression.Constant(this.comparand), Expression.Constant(StringComparison.OrdinalIgnoreCase));
                                return Expression.NotEqual(indexOfCall, Expression.Constant(-1));
                            case Predicate.DoesNotContain:
                                // fieldValue.ToString(CultureInfo.InvariantCulture).IndexOf(this.comparand, StringComparison.OrdinalIgnoreCase) == -1
                                toStringCall = Expression.Call(fieldConvertedExpression, DoubleToStringMethodInfo, Expression.Constant(CultureInfo.InvariantCulture));
                                indexOfCall = Expression.Call(toStringCall, StringIndexOfMethodInfo, Expression.Constant(this.comparand), Expression.Constant(StringComparison.OrdinalIgnoreCase));
                                return Expression.Equal(indexOfCall, Expression.Constant(-1));
                            default:
                                this.ThrowOnInvalidFilter(fieldType);
                                break;
                        }
                    }

                    break;
                case TypeCode.String:
                    {
                        Expression fieldValueOrEmptyString = Expression.Condition(Expression.Equal(fieldExpression, Expression.Constant(null)), Expression.Constant(string.Empty), fieldExpression);

                        Expression indexOfCall = Expression.Call(fieldValueOrEmptyString, StringIndexOfMethodInfo, Expression.Constant(this.comparand), Expression.Constant(StringComparison.OrdinalIgnoreCase));

                        switch (this.predicate)
                        {
                            case Predicate.Equal:
                                // (fieldValue ?? string.Empty).Equals(this.comparand, StringComparison.OrdinalIgnoreCase)
                                return Expression.Call(fieldValueOrEmptyString, StringEqualsMethodInfo, Expression.Constant(this.comparand), Expression.Constant(StringComparison.OrdinalIgnoreCase));
                            case Predicate.NotEqual:
                                // !(fieldValue ?? string.Empty).Equals(this.comparand, StringComparison.OrdinalIgnoreCase)
                                return Expression.Not(Expression.Call(fieldValueOrEmptyString, StringEqualsMethodInfo, Expression.Constant(this.comparand), Expression.Constant(StringComparison.OrdinalIgnoreCase)));
                            case Predicate.LessThan:
                                // double.Parse(fieldValue) < comparandDouble
                                this.ThrowOnInvalidFilter(fieldType, !this.comparandDouble.HasValue);
                                MethodCallExpression fieldParsedIntoDouble = Expression.Call(DoubleParseMethodInfo, fieldExpression, Expression.Constant(CultureInfo.InvariantCulture));
                                return Expression.LessThan(fieldParsedIntoDouble, Expression.Constant(this.comparandDouble.Value));
                            case Predicate.GreaterThan:
                                // double.Parse(fieldValue) > comparandDouble
                                this.ThrowOnInvalidFilter(fieldType, !this.comparandDouble.HasValue);
                                fieldParsedIntoDouble = Expression.Call(DoubleParseMethodInfo, fieldExpression, Expression.Constant(CultureInfo.InvariantCulture));
                                return Expression.GreaterThan(fieldParsedIntoDouble, Expression.Constant(this.comparandDouble.Value));
                            case Predicate.LessThanOrEqual:
                                // double.Parse(fieldValue) <= comparandDouble
                                this.ThrowOnInvalidFilter(fieldType, !this.comparandDouble.HasValue);
                                fieldParsedIntoDouble = Expression.Call(DoubleParseMethodInfo, fieldExpression, Expression.Constant(CultureInfo.InvariantCulture));
                                return Expression.LessThanOrEqual(fieldParsedIntoDouble, Expression.Constant(this.comparandDouble.Value));
                            case Predicate.GreaterThanOrEqual:
                                // double.Parse(fieldValue) >= comparandDouble
                                this.ThrowOnInvalidFilter(fieldType, !this.comparandDouble.HasValue);
                                fieldParsedIntoDouble = Expression.Call(DoubleParseMethodInfo, fieldExpression, Expression.Constant(CultureInfo.InvariantCulture));
                                return Expression.GreaterThanOrEqual(fieldParsedIntoDouble, Expression.Constant(this.comparandDouble.Value));
                            case Predicate.Contains:
                                // fieldValue => (fieldValue ?? string.Empty).IndexOf(this.comparand, StringComparison.OrdinalIgnoreCase) != -1;
                                return Expression.NotEqual(indexOfCall, Expression.Constant(-1));
                            case Predicate.DoesNotContain:
                                // fieldValue => (fieldValue ?? string.Empty).IndexOf(this.comparand, StringComparison.OrdinalIgnoreCase) == -1;
                                return Expression.Equal(indexOfCall, Expression.Constant(-1));
                            default:
                                this.ThrowOnInvalidFilter(fieldType);
                                break;
                        }
                    }

                    break;
                default:
                    if (fieldType == typeof(bool?))
                    {
                        this.ThrowOnInvalidFilter(fieldType, !this.comparandBoolean.HasValue && !string.Equals(this.comparand, "null", StringComparison.OrdinalIgnoreCase));

                        switch (this.predicate)
                        {
                            case Predicate.Equal:
                                Func<bool?, bool> comparator = fieldValue => fieldValue == this.comparandBoolean;
                                return Expression.Call(Expression.Constant(comparator.Target), comparator.Method, fieldExpression);
                            case Predicate.NotEqual:
                                comparator = fieldValue => fieldValue != this.comparandBoolean;
                                return Expression.Call(Expression.Constant(comparator.Target), comparator.Method, fieldExpression);
                            default:
                                this.ThrowOnInvalidFilter(fieldType);
                                break;
                        }
                    }
                    else if (fieldType == typeof(TimeSpan))
                    {
                        this.ThrowOnInvalidFilter(fieldType, !this.comparandTimeSpan.HasValue);

                        switch (this.predicate)
                        {
                            case Predicate.Equal:
                                Func<TimeSpan, bool> comparator = fieldValue => fieldValue == this.comparandTimeSpan.Value;
                                return Expression.Call(Expression.Constant(comparator.Target), comparator.Method, fieldExpression);
                            case Predicate.NotEqual:
                                comparator = fieldValue => fieldValue != this.comparandTimeSpan.Value;
                                return Expression.Call(Expression.Constant(comparator.Target), comparator.Method, fieldExpression);
                            case Predicate.LessThan:
                                comparator = fieldValue => fieldValue < this.comparandTimeSpan.Value;
                                return Expression.Call(Expression.Constant(comparator.Target), comparator.Method, fieldExpression);
                            case Predicate.GreaterThan:
                                comparator = fieldValue => fieldValue > this.comparandTimeSpan.Value;
                                return Expression.Call(Expression.Constant(comparator.Target), comparator.Method, fieldExpression);
                            case Predicate.LessThanOrEqual:
                                comparator = fieldValue => fieldValue <= this.comparandTimeSpan.Value;
                                return Expression.Call(Expression.Constant(comparator.Target), comparator.Method, fieldExpression);
                            case Predicate.GreaterThanOrEqual:
                                comparator = fieldValue => fieldValue >= this.comparandTimeSpan.Value;
                                return Expression.Call(Expression.Constant(comparator.Target), comparator.Method, fieldExpression);
                            default:
                                this.ThrowOnInvalidFilter(fieldType);
                                break;
                        }
                    }
                    else if (fieldType == typeof(Uri))
                    {
                        Expression toStringCall = Expression.Call(fieldExpression, UriToStringMethodInfo);

                        Expression fieldValueOrEmptyString = Expression.Condition(Expression.Equal(fieldExpression, Expression.Constant(null)), Expression.Constant(string.Empty), toStringCall);

                        Expression indexOfCall = Expression.Call(fieldValueOrEmptyString, StringIndexOfMethodInfo, Expression.Constant(this.comparand), Expression.Constant(StringComparison.OrdinalIgnoreCase));

                        switch (this.predicate)
                        {
                            case Predicate.Equal:
                                // (fieldValue?.ToString() ?? string.Empty).Equals(this.comparand, StringComparison.OrdinalIgnoreCase)
                                return Expression.Call(fieldValueOrEmptyString, StringEqualsMethodInfo, Expression.Constant(this.comparand), Expression.Constant(StringComparison.OrdinalIgnoreCase));
                            case Predicate.NotEqual:
                                // !(fieldValue?.ToString() ?? string.Empty).Equals(this.comparand, StringComparison.OrdinalIgnoreCase)
                                return Expression.Not(Expression.Call(fieldValueOrEmptyString, StringEqualsMethodInfo, Expression.Constant(this.comparand), Expression.Constant(StringComparison.OrdinalIgnoreCase)));
                            case Predicate.Contains:
                                // fieldValue => (fieldValue?.ToString() ?? string.Empty).IndexOf(this.comparand, StringComparison.OrdinalIgnoreCase) != -1;
                                return Expression.NotEqual(indexOfCall, Expression.Constant(-1));
                            case Predicate.DoesNotContain:
                                // fieldValue => (fieldValue?.ToString() ?? string.Empty).IndexOf(this.comparand, StringComparison.OrdinalIgnoreCase) == -1;
                                return Expression.Equal(indexOfCall, Expression.Constant(-1));
                            default:
                                this.ThrowOnInvalidFilter(fieldType);
                                break;
                        }
                    }
                    else
                    {
                        this.ThrowOnInvalidFilter(fieldType);
                    }

                    break;
            }

            return null;
        }

        private Expression ProduceComparatorExpression(ParameterExpression documentExpression)
        {
            // this.predicate is either Predicate.Contains or Predicate.DoesNotContain at this point
            Expression comparisonExpression = this.predicate == Predicate.Contains ? Expression.Constant(false) : Expression.Constant(true);

            foreach (PropertyInfo propertyInfo in typeof(TTelemetry).GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                try
                {
                    Expression propertyComparatorExpression;
                    if (string.Equals(propertyInfo.Name, CustomDimensionsPropertyName, StringComparison.Ordinal))
                    {
                        // ScanDictionary(document.<CustomDimensionsPropertyName>, <this.comparand>)
                        MemberExpression customDimensionsProperty = Expression.Property(documentExpression, CustomDimensionsPropertyName);
                        propertyComparatorExpression = Expression.Call(
                            null,
                            DictionaryStringStringScanMethodInfo,
                            customDimensionsProperty,
                            Expression.Constant(this.comparand));

                        if (this.predicate == Predicate.DoesNotContain)
                        {
                            propertyComparatorExpression = Expression.Not(propertyComparatorExpression);
                        }
                    }
                    else if (string.Equals(propertyInfo.Name, CustomMetricsPropertyName, StringComparison.Ordinal))
                    {
                        // ScanDictionary(document.<CustomMetricsPropertyName>, <this.comparand>)
                        MemberExpression customMetricsProperty = Expression.Property(documentExpression, CustomMetricsPropertyName);
                        propertyComparatorExpression = Expression.Call(
                            null,
                            DictionaryStringDoubleScanMethodInfo,
                            customMetricsProperty,
                            Expression.Constant(this.comparand));

                        if (this.predicate == Predicate.DoesNotContain)
                        {
                            propertyComparatorExpression = Expression.Not(propertyComparatorExpression);
                        }
                    }
                    else
                    {
                        // regular property, create a comparator
                        Expression fieldExpression = ProduceFieldExpression(documentExpression, propertyInfo.Name, FieldNameType.FieldName);
                        propertyComparatorExpression = this.ProduceComparatorExpression(fieldExpression, propertyInfo.PropertyType);
                    }

                    comparisonExpression = this.predicate == Predicate.Contains
                                               ? Expression.OrElse(comparisonExpression, propertyComparatorExpression)
                                               : Expression.AndAlso(comparisonExpression, (propertyComparatorExpression));
                }
                catch (Exception e)
                {
                    // probably an unsupported property (e.g. bool), ignore and continue
                }
            }

            return comparisonExpression;
        }

        private void ThrowOnInvalidFilter(Type fieldType, bool conditionToThrow = true)
        {
            if (conditionToThrow)
            {
                throw new ArgumentOutOfRangeException(string.Format(CultureInfo.InvariantCulture, "The filter is invalid. Field: '{0}', field type: '{1}', predicate: '{2}', comparand: '{3}', document type: '{4}'", this.fieldName, fieldType?.FullName ?? "---", this.predicate, this.comparand, typeof(TTelemetry).FullName));
            }
        }

        internal enum FieldNameType
        {
            FieldName,

            CustomMetricName,

            CustomDimensionName,

            AnyField
        }
    }
}