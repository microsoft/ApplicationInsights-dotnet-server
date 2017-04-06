namespace Microsoft.ApplicationInsights.Web.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    internal class HttpRoutesData
    {
        internal GetHttpRouteTemplateName()
        {
            string name = string.Empty;

            object val;
            object[] routes = null;

            // at runtime this can't cast directly to IHttpRouteData because there are multiple versions of webapi
            if (routeValues.TryGetValue("MS_SubRoutes", out val) && (routes = val as object[]) != null && routes.Length > 0)
            {
                object first = routes.OrderBy(GetPrecedenceForRoute).FirstOrDefault();

                // through reflection:
                var getter = first?.GetType().GetProperty("Route")?.GetGetMethod();
                if (getter != null)
                {
                    var route = getter.Invoke(first, null);
                    var templateGetter = route?.GetType().GetProperty("RouteTemplate")?.GetGetMethod();
                    if (templateGetter != null)
                    {
                        var routeTemplate = templateGetter.Invoke(route, null) as string;
                        if (!string.IsNullOrEmpty(routeTemplate))
                        {
                            name = routeTemplate;
                        }
                    }
                }
            }

            return name;
        }

        private static decimal GetPrecedenceForRoute(object o)
        {
            decimal precedence = 1;

            var getter = o?.GetType().GetProperty("Route")?.GetGetMethod();
            if (getter != null)
            {
                var route = getter.Invoke(o, null);

                var dataTokensGetter = route?.GetType().GetProperty("DataTokens")?.GetGetMethod();

                if (dataTokensGetter != null)
                {
                    var dataTokens = dataTokensGetter.Invoke(route, null) as IDictionary<string, object>;
                    object p = null;
                    if ((dataTokens?.TryGetValue("precedence", out p) ?? false) && p is decimal)
                    {
                        precedence = (decimal)p;
                    }
                }
            }

            return precedence;
        }

        /// <summary>
        /// Gets the property value.
        /// </summary>
        /// <param name="targetObject">The target object.</param>
        /// <param name="targetType">The target type.</param>
        /// <param name="name">The property name.</param>
        /// <returns>The property value.</returns>
        private object GetPropertyValue(object targetObject, Type targetType, string name)
        {
            if (string.IsNullOrEmpty(name) == true)
            {
                throw new ArgumentNullException("name");
            }

            PropertyInfo info = targetType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
            if (info == null)
            {
                throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, "Could not get property info for '{0}' property of {1} type.", name, targetType.FullName));
            }

            return info.GetValue(targetObject, null);
        }
    }
}
