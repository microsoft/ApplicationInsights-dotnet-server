namespace Microsoft.ApplicationInsights
{
    using System;

    using Microsoft.VisualStudio.TestTools.UnitTesting;

    internal static partial class QuickPulseTestHelper
    {
        public static object GetPrivateField(object obj, string fieldName)
        {
            var t = new PrivateObject(obj);
            return t.GetField(fieldName);
        }

        public static void SetPrivateStaticField(Type type, string fieldName, object value)
        {
            PrivateType t = new PrivateType(type);
            t.SetStaticField(fieldName, value);
        }
    }
}