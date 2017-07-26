namespace Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.Tests
{
    using System;

    internal static class ArrayHelpers
    {
        public static void ForEach<T>(T[] array, Action<T> action)
        {
            foreach (T item in array)
            {
                action(item);
            }
        }
    }
}
