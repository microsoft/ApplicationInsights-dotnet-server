namespace Unit.Tests
{
    using System;

    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.DataContracts;

    class TelemetryMock : ITelemetry
    {
        public bool BooleanField { get; set; }

        public int IntField { get; set; }

        public float FloatField { get; set; }

        public double DoubleField { get; set; }

        public string StringField { get; set; }

        public void Sanitize()
        {
            throw new NotImplementedException();
        }

        public DateTimeOffset Timestamp { get; set; }

        public TelemetryContext Context { get; }

        public string Sequence { get; set; }
    }
}
