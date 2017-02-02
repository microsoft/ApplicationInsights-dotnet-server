using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.ApplicationInsights.Extensibility.Filtering
{
    using System.Linq;
    using System.Linq.Expressions;
    using System.Net.PeerToPeer;

    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.DataContracts;

    using FilterLambda = Func<Microsoft.ApplicationInsights.Channel.ITelemetry, object>;

    public class Filter
    {
        private static Type[] supportedTypes =
            {
                //typeof(RequestTelemetry), typeof(DependencyTelemetry), typeof(ExceptionTelemetry),
                //typeof(EventTelemetry), typeof(MetricTelemetry), typeof(PerformanceCounterTelemetry)
            };

        private readonly Dictionary<Type, FilterLambda> filterLambdas = new Dictionary<Type, FilterLambda>();

        public Filter(FilterInfo filterInfo, Type additionalSupportedType = null)
        {
            if (additionalSupportedType != null && supportedTypes.All(type => type != additionalSupportedType))
            {
                supportedTypes = supportedTypes.Concat(new[] { additionalSupportedType }).ToArray();
            }

            foreach (Type supportedType in supportedTypes)
            {
                try
                {
                    ParameterExpression parameterExpression = Expression.Variable(supportedType);

                    MemberExpression fieldExpression = Expression.Property(parameterExpression, filterInfo.FieldName);

                    Expression<Func<RequestTelemetry, object>> lambdaExpression = Expression.Lambda<Func<RequestTelemetry, object>>(fieldExpression, parameterExpression);

                    Func<RequestTelemetry, object> compiledLambda = lambdaExpression.Compile();

                    Func<ITelemetry, object> convertedLambda = compiledLambda as Func<ITelemetry, object>;

                    //!!! call to check if it runs ok

                    this.filterLambdas.Add(supportedType, convertedLambda);
                }
                catch (Exception e)
                {
                    // couldn't create the filter for the given supported type

                    //!!! report error if none created for any supported types
                }
            }
        }

        public object Check(ITelemetry document)
        {
            Type documentType = document.GetType();

            FilterLambda filterLambda;
            if (this.filterLambdas.TryGetValue(documentType, out filterLambda))
            {
                try
                {
                    return filterLambda(document);
                }
                catch (Exception)
                {
                    //!!! report error?
                    return false;
                }
            }

            //!!! unsupported type. Report?
            return false;
        }
    }
}
