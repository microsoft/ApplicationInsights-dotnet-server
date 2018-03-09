namespace Microsoft.ApplicationInsights.WindowsServer
{
    using System;
    using System.IO;
    using System.Net;
    using System.Runtime.Serialization.Json;
    using System.Text;
    using System.Threading;
    using Microsoft.ApplicationInsights.WindowsServer.Implementation;
    using Microsoft.ApplicationInsights.WindowsServer.Implementation.DataContracts;
    using Microsoft.ApplicationInsights.WindowsServer.Mock;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Assert = Xunit.Assert;

    [TestClass]
    public class AzureInstanceMetadataEndToEndTests
    {
        private MemoryStream jsonStream;

        public AzureInstanceMetadataEndToEndTests()
        {
            this.TestComputeMetadata = new AzureInstanceComputeMetadata()
            {
                Location = "US-West",
                Name = "test-vm01",
                Offer = "D9_USWest",
                OsType = "Linux",
                PlacementGroupId = "placement-grp",
                PlatformFaultDomain = "0",
                PlatformUpdateDomain = "0",
                Publisher = "Microsoft",
                ResourceGroupName = "test.resource-group_01",
                Sku = "Windows_10",
                SubscriptionId = Guid.NewGuid().ToString(),
                Tags = "thisTag;thatTag",
                Version = "10.8a",
                VmId = Guid.NewGuid().ToString(),
                VmSize = "A8"
            };

            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(AzureInstanceComputeMetadata));
            this.jsonStream = new MemoryStream();
            serializer.WriteObject(this.jsonStream, this.TestComputeMetadata);
        }

        private AzureInstanceComputeMetadata TestComputeMetadata { get; set; }

        [TestMethod]
        public void SpoofedResponseFromAzureIMSDoesntCrash()
        {
            CancellationTokenSource cts = new CancellationTokenSource();

            string mockUri = "http://localhost:9922/";

            using (new AzureInstanceMetadataServiceMock(
                mockUri, 
                (HttpListenerContext context) =>
            {
                HttpListenerResponse response = context.Response;

                // Construct a response.
                response.ContentEncoding = Encoding.UTF8;

                // Get a response stream and write the response to it.
                this.jsonStream.Position = 0;
                response.ContentLength64 = (int)this.jsonStream.Length;
                this.jsonStream.Position = 0;
                context.Response.ContentType = "application/json";
                this.jsonStream.WriteTo(context.Response.OutputStream);
                context.Response.StatusCode = 200;
            }))
            {
                var azureIms = new AzureMetadataRequestor
                {
                    BaseAimsUri = mockUri
                };

                var azureImsProps = new AzureComputeMetadataHeartbeatPropertyProvider();
                var azureIMSData = azureIms.GetAzureComputeMetadataAsync();
                azureIMSData.Wait();

                foreach (string fieldName in azureImsProps.ExpectedAzureImsFields)
                {
                    string fieldValue = azureIMSData.Result.GetValueForField(fieldName);
                    Assert.NotNull(fieldValue);
                    Assert.Equal(fieldValue, this.TestComputeMetadata.GetValueForField(fieldName));
                }
            }
        }

        [TestMethod]
        public void AzureImsResponseTooLargeStopsCollection()
        {
            CancellationTokenSource cts = new CancellationTokenSource();

            string mockUri = "http://localhost:9922/";

            using (new AzureInstanceMetadataServiceMock(
                mockUri, 
                (HttpListenerContext context) =>
            {
                HttpListenerResponse response = context.Response;

                // Construct a response just like in the positive test but triple it.
                response.ContentEncoding = Encoding.UTF8;

                // Get a response stream and write the response to it.
                this.jsonStream.Position = 0;
                response.ContentLength64 = 3 * (int)this.jsonStream.Length;
                this.jsonStream.Position = 0;
                context.Response.ContentType = "application/json";

                this.jsonStream.WriteTo(context.Response.OutputStream);
                this.jsonStream.Position = 0;
                this.jsonStream.WriteTo(context.Response.OutputStream);
                this.jsonStream.Position = 0;
                this.jsonStream.WriteTo(context.Response.OutputStream);

                context.Response.StatusCode = 200;
            }))
            {
                var azureIms = new AzureMetadataRequestor
                {
                    BaseAimsUri = mockUri
                };

                var azureIMSData = azureIms.GetAzureComputeMetadataAsync();
                azureIMSData.Wait();

                Assert.Null(azureIMSData.Result);
            }
        }

        [TestMethod]
        public void AzureImsResponseExcludesMalformedValues()
        {
            CancellationTokenSource cts = new CancellationTokenSource();

            string mockUri = "http://localhost:9922/";

            using (new AzureInstanceMetadataServiceMock(
                mockUri, 
                (HttpListenerContext context) =>
            {
                HttpListenerResponse response = context.Response;

                var malformedData = this.TestComputeMetadata;
                malformedData.Name = "Not allowed for VM names";
                malformedData.ResourceGroupName = "Not allowed for resource group name";
                malformedData.SubscriptionId = "Definitely-not-a GUID up here";
                var serializer = new DataContractJsonSerializer(typeof(AzureInstanceComputeMetadata));
                var malformedJsonStream = new MemoryStream();
                serializer.WriteObject(malformedJsonStream, malformedData);

                // Construct a response just like in the positive test but triple it.
                response.ContentEncoding = Encoding.UTF8;

                // Get a response stream and write the response to it.
                response.ContentLength64 = (int)malformedJsonStream.Length;
                context.Response.ContentType = "application/json";
                malformedJsonStream.WriteTo(context.Response.OutputStream);
                context.Response.StatusCode = 200;
            }))
            {
                var azureIms = new AzureMetadataRequestor
                {
                    BaseAimsUri = mockUri
                };

                var azureImsProps = new AzureComputeMetadataHeartbeatPropertyProvider(azureIms);
                var hbeatProvider = new HeartbeatProviderMock();
                var azureIMSData = azureImsProps.SetDefaultPayloadAsync(hbeatProvider);
                azureIMSData.Wait();

                Assert.Empty(hbeatProvider.HbeatProps["azInst_name"]);
                Assert.Empty(hbeatProvider.HbeatProps["azInst_resourceGroupName"]);
                Assert.Empty(hbeatProvider.HbeatProps["azInst_subscriptionId"]);
            }
        }

        [TestMethod]
        public void AzureImsResponseTimesOut()
        {
            CancellationTokenSource cts = new CancellationTokenSource();

            string mockUri = "http://localhost:9922/";

            using (new AzureInstanceMetadataServiceMock(
                mockUri, 
                (HttpListenerContext context) =>
            {
                // wait for longer than the request timeout
                System.Threading.Thread.Sleep(TimeSpan.FromSeconds(5));

                HttpListenerResponse response = context.Response;

                // Construct a response just like in the positive test but triple it.
                response.ContentEncoding = Encoding.UTF8;

                // Get a response stream and write the response to it.
                this.jsonStream.Position = 0;
                response.ContentLength64 = (int)this.jsonStream.Length;
                context.Response.ContentType = "application/json";
                this.jsonStream.WriteTo(context.Response.OutputStream);

                context.Response.StatusCode = 200;
            }))
            {
                var azureIms = new AzureMetadataRequestor
                {
                    BaseAimsUri = mockUri,
                    AzureImsRequestTimeout = TimeSpan.FromSeconds(1)
                };

                var azureIMSData = azureIms.GetAzureComputeMetadataAsync();
                azureIMSData.Wait();

                Assert.Null(azureIMSData.Result);
            }
        }

        [TestMethod]
        public void AzureImsResponseUnsuccessful()
        {
            CancellationTokenSource cts = new CancellationTokenSource();

            string mockUri = "http://localhost:9922/";

            using (new AzureInstanceMetadataServiceMock(
                mockUri, 
                (HttpListenerContext context) =>
            {
                context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            }))
            {
                var azureIms = new AzureMetadataRequestor
                {
                    BaseAimsUri = mockUri
                };

                var azureIMSData = azureIms.GetAzureComputeMetadataAsync();
                azureIMSData.Wait();

                Assert.Null(azureIMSData.Result);
            }
        }
    }
}
