namespace Microsoft.ApplicationInsights.WindowsServer
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net.Http;
    using System.Runtime.Serialization.Json;
    using System.Text;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.ApplicationInsights.WindowsServer.Implementation;
    using Microsoft.ApplicationInsights.WindowsServer.Implementation.DataContracts;
    using Microsoft.ApplicationInsights.WindowsServer.Mock;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Assert = Xunit.Assert;

    [TestClass]
    public class AzureInstanceMetadataEndToEndTests
    {
        private AzureInstanceComputeMetadata Data { get; set; }
        private MemoryStream JsonStream;

        public AzureInstanceMetadataEndToEndTests()
        {
            this.Data = new AzureInstanceComputeMetadata()
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
            this.JsonStream = new MemoryStream();
            serializer.WriteObject(this.JsonStream, this.Data);
        }

        [TestMethod]
        public void SpoofedResponseFromAzureIMSDoesntCrash()
        {
            CancellationTokenSource cts = new CancellationTokenSource();

            string mockUri = "http://localhost:9922/";

            using (new LocalServer(mockUri, (HttpListenerContext context) =>
            {
                HttpListenerResponse response = context.Response;

                // Construct a response.
                response.ContentEncoding = Encoding.UTF8;
                // Get a response stream and write the response to it.
                this.JsonStream.Position = 0;
                response.ContentLength64 = (int)this.JsonStream.Length;
                this.JsonStream.Position = 0;
                context.Response.ContentType = "application/json";
                this.JsonStream.WriteTo(context.Response.OutputStream);
                context.Response.StatusCode = 200;
            }))
            {
                var azIms = new AzureMetadataRequestor
                {
                    BaseAimsUri = mockUri
                };

                var azImsProps = new AzureComputeMetadataHeartbeatPropertyProvider();
                var azureIMSData = azIms.GetAzureComputeMetadataAsync();
                azureIMSData.Wait();

                foreach (string fieldName in azImsProps.ExpectedAzureImsFields)
                {
                    string fieldValue = azureIMSData.Result.GetValueForField(fieldName);
                    Assert.NotNull(fieldValue);
                    Assert.Equal(fieldValue, this.Data.GetValueForField(fieldName));
                }
            }
        }

        class LocalServer : IDisposable
        {
            private readonly HttpListener listener;
            private readonly CancellationTokenSource cts;

            public LocalServer(string url, Action<HttpListenerContext> onRequest = null)
            {
                this.listener = new HttpListener();
                this.listener.Prefixes.Add(url);
                this.listener.Start();
                this.cts = new CancellationTokenSource();

                Task.Run(
                    () =>
                    {
                        if (!this.cts.IsCancellationRequested)
                        {
                            HttpListenerContext context = this.listener.GetContext();
                            if (onRequest != null)
                            {
                                onRequest(context);
                            }
                            else
                            {
                                context.Response.StatusCode = 200;
                            }

                            context.Response.OutputStream.Close();
                            context.Response.Close();
                        }
                    },
                    this.cts.Token);
            }

            public void Dispose()
            {
                this.cts.Cancel(false);
                this.listener.Abort();
                ((IDisposable)this.listener).Dispose();
                this.cts.Dispose();
            }
        }

    }
}
