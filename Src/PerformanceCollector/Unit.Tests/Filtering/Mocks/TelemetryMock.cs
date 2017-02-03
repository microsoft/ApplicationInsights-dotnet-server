namespace Unit.Tests
{
    using System;

    using Microsoft.ApplicationInsights.Channel;
    using Microsoft.ApplicationInsights.DataContracts;

    class TelemetryMock : ITelemetry
    {
        public string Field { get; set; }

        public void Sanitize()
        {
            throw new NotImplementedException();
        }

        public DateTimeOffset Timestamp { get; set; }

        public TelemetryContext Context { get; }

        public string Sequence { get; set; }
    }
}
