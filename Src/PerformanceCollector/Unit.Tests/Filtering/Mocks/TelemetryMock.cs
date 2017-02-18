namespace Unit.Tests
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.DataContracts;

    class TelemetryMock : ITelemetry
    {
        public bool BooleanField { get; set; }

        public bool? NullableBooleanField { get; set; }

        public int IntField { get; set; }

        public double DoubleField { get; set; }

        public string StringField { get; set; }

        public TimeSpan TimeSpanField { get; set; }

        public IDictionary<string, string> Properties { get; } = new Dictionary<string, string>();
        
        public IDictionary<string, double> Metrics { get; } = new Dictionary<string, double>();
        
        public DateTimeOffset Timestamp { get; set; }

        public TelemetryContext Context { get; }

        public string Sequence { get; set; }

        public void Sanitize()
        {
            throw new NotImplementedException();
        }
    }
}
