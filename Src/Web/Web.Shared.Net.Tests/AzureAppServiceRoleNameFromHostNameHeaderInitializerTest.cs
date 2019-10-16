namespace Microsoft.ApplicationInsights.Web
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.Remoting.Messaging;
    using System.Web;

    using Microsoft.ApplicationInsights.Common;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.Web.Helpers;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class AzureAppServiceRoleNameFromHostNameHeaderInitializerTest
    {
        [TestMethod]
        public void InitializeDoesNotThrowIfHttpContextIsUnavailable()
        {
            Func<HttpContext> nullContext = () => { return null; };

            var initializer = new TestableAzureAppServiceRoleNameFromHostNameHeaderInitializer(null, nullContext);

            initializer.Initialize(new RequestTelemetry());
        }

        [TestMethod]
        public void InitializeFallsbackToEnvIfHttpContextIsUnavailable()
        {
            try
            {
                Environment.SetEnvironmentVariable("WEBSITE_HOSTNAME", "RoleNameEnv");
                Func<HttpContext> nullContext = () => { return null; };

                var initializer = new TestableAzureAppServiceRoleNameFromHostNameHeaderInitializer(null, nullContext);
                var req = new RequestTelemetry();
                initializer.Initialize(req);

                Assert.AreEqual("RoleNameEnv", req.Context.Cloud.RoleName);
            }
            finally
            {
                Environment.SetEnvironmentVariable("WEBSITE_HOSTNAME", null);
            }
        }

        [TestMethod]
        public void InitializeFallsbackToEnvIfHttpContextIsUnavailableWithAzureWebsitesHostnameending()
        {
            try
            {
                Environment.SetEnvironmentVariable("WEBSITE_HOSTNAME", "RoleNameEnv.azurewebsites.net");
                Func<HttpContext> nullContext = () => { return null; };

                var initializer = new TestableAzureAppServiceRoleNameFromHostNameHeaderInitializer(null, nullContext);
                var req = new RequestTelemetry();
                initializer.Initialize(req);

                Assert.AreEqual("RoleNameEnv", req.Context.Cloud.RoleName);
            }
            finally
            {
                Environment.SetEnvironmentVariable("WEBSITE_HOSTNAME", null);
            }
        }

        [TestMethod]
        public void InitializeDoesNotThrowIfHostNameHeaderIsEmpty()
        {
            var eventTelemetry = new EventTelemetry("name");
            var source = new TestableAzureAppServiceRoleNameFromHostNameHeaderInitializer(new Dictionary<string, string>
                {
                    { "WAS-DEFAULT-HOSTNAME", string.Empty }
                });

            source.Initialize(eventTelemetry);
        }

        [TestMethod]
        public void InitializeFallsbackToEnvIfHostNameHeaderIsEmpty()
        {
            try
            {
                Environment.SetEnvironmentVariable("WEBSITE_HOSTNAME", "RoleNameEnv");
                var eventTelemetry = new EventTelemetry("name");
                var source = new TestableAzureAppServiceRoleNameFromHostNameHeaderInitializer(new Dictionary<string, string>
                {
                    { "WAS-DEFAULT-HOSTNAME", string.Empty }
                });

                source.Initialize(eventTelemetry);
                Assert.AreEqual("RoleNameEnv", eventTelemetry.Context.Cloud.RoleName);
            }
            finally
            {
                Environment.SetEnvironmentVariable("WEBSITE_HOSTNAME", null);
            }
        }

        [TestMethod]
        public void InitializeDoesNotThrowIfHostNameHeaderIsNull()
        {
            var eventTelemetry = new EventTelemetry("name");
            var source = new TestableAzureAppServiceRoleNameFromHostNameHeaderInitializer(new Dictionary<string, string>
                {
                    { "WAS-DEFAULT-HOSTNAME", null }
                });

            source.Initialize(eventTelemetry);
        }

        [TestMethod]
        public void InitializeFallsbackToEnvIfHostNameHeaderIsNull()
        {
            try
            {
                Environment.SetEnvironmentVariable("WEBSITE_HOSTNAME", "RoleNameEnv");
                var eventTelemetry = new EventTelemetry("name");
                var source = new TestableAzureAppServiceRoleNameFromHostNameHeaderInitializer(new Dictionary<string, string>
                {
                    { "WAS-DEFAULT-HOSTNAME", null }
                });

                source.Initialize(eventTelemetry);
                Assert.AreEqual("RoleNameEnv", eventTelemetry.Context.Cloud.RoleName);
            }
            finally
            {
                Environment.SetEnvironmentVariable("WEBSITE_HOSTNAME", null);
            }
        }

        [TestMethod]
        public void InitializeSetsRoleNameFromHostNameHeader()
        {
            var eventTelemetry = new EventTelemetry("name");
            var source = new TestableAzureAppServiceRoleNameFromHostNameHeaderInitializer(new Dictionary<string, string>
                {
                    { "WAS-DEFAULT-HOSTNAME", "RoleNameEnv" }
                });

            source.Initialize(eventTelemetry);

            Assert.AreEqual("RoleNameEnv", eventTelemetry.Context.Cloud.RoleName);
        }

        [TestMethod]
        public void InitializeSetsRoleNameFromHostNameHeaderEndingInAzureWebSites()
        {
            var eventTelemetry = new EventTelemetry("name");
            var source = new TestableAzureAppServiceRoleNameFromHostNameHeaderInitializer(new Dictionary<string, string>
                {
                    { "WAS-DEFAULT-HOSTNAME", "RoleNameEnv.azurewebsites.net" }
                });

            source.Initialize(eventTelemetry);

            Assert.AreEqual("RoleNameEnv", eventTelemetry.Context.Cloud.RoleName);
        }

        [TestMethod]
        public void InitializeSetsRoleNameFromRequestTelemetryIfPresent()
        {
            var requestTelemetry = new RequestTelemetry();
            requestTelemetry.Context.Cloud.RoleName = "RoleNameOnRequest";

            Func<HttpContext, RequestTelemetry> requestFromContext = (ctx) => { return requestTelemetry; };

            var eventTelemetry = new EventTelemetry("name");
            var source = new TestableAzureAppServiceRoleNameFromHostNameHeaderInitializer(new Dictionary<string, string>
                {
                    { "WAS-DEFAULT-HOSTNAME", "RoleNameFromHostHeader" }
                }, getRequestFromContext: requestFromContext);

            source.Initialize(eventTelemetry);

            Assert.AreEqual("RoleNameOnRequest", eventTelemetry.Context.Cloud.RoleName);
        }

        [TestMethod]
        public void InitializeSavesRoleNameIntoRequestFromHostNameHeader()
        {
            var requestTelemetry = new RequestTelemetry();            
            Func<HttpContext, RequestTelemetry> requestFromContext = (ctx) => { return requestTelemetry; };

            var eventTelemetry = new EventTelemetry("name");
            var source = new TestableAzureAppServiceRoleNameFromHostNameHeaderInitializer(new Dictionary<string, string>
                {
                    { "WAS-DEFAULT-HOSTNAME", "RoleNameFromHostHeader" }
                }, getRequestFromContext: requestFromContext);

            source.Initialize(eventTelemetry);

            Assert.AreEqual("RoleNameFromHostHeader", eventTelemetry.Context.Cloud.RoleName);
            Assert.AreEqual("RoleNameFromHostHeader", requestTelemetry.Context.Cloud.RoleName);
        }

        [TestMethod]
        public void InitializeSetsRoleNameFromHostNameWithAzureWebsitesCustom()
        {
            var eventTelemetry = new EventTelemetry("name");
            var source = new TestableAzureAppServiceRoleNameFromHostNameHeaderInitializer(new Dictionary<string, string>
                {
                    { "WAS-DEFAULT-HOSTNAME", "appserviceslottest-ppe.azurewebsites.us" }
                });

            source.WebAppSuffix = ".azurewebsites.us";
            source.Initialize(eventTelemetry);

            Assert.AreEqual("appserviceslottest-ppe", eventTelemetry.Context.Cloud.RoleName);
        }

        [TestMethod]
        public void InitializeSetsRoleNameFromEnvWithAzureWebsitesCustom()
        {
            try
            {
                Environment.SetEnvironmentVariable("WEBSITE_HOSTNAME", "appserviceslottest-ppe.azurewebsites.us");
                var eventTelemetry = new EventTelemetry("name");
                var source = new TestableAzureAppServiceRoleNameFromHostNameHeaderInitializer(".azurewebsites.us");

                source.Initialize(eventTelemetry);
                Assert.AreEqual("appserviceslottest-ppe", eventTelemetry.Context.Cloud.RoleName);
            }
            finally
            {
                Environment.SetEnvironmentVariable("WEBSITE_HOSTNAME", null);
            }
        }

        [TestMethod]
        public void InitializeDoesNotOverrideRoleName()
        {
            var requestTelemetry = new RequestTelemetry();
            requestTelemetry.Context.Cloud.RoleName = "ExistingRoleName";
            var source = new TestableAzureAppServiceRoleNameFromHostNameHeaderInitializer(new Dictionary<string, string>
                {
                    { "WAS-DEFAULT-HOSTNAME", "appserviceslottest-ppe.azurewebsites.us" }
                });
            source.Initialize(requestTelemetry);

            Assert.AreEqual("ExistingRoleName", requestTelemetry.Context.Cloud.RoleName);
        }

        private class TestableAzureAppServiceRoleNameFromHostNameHeaderInitializer : AzureAppServiceRoleNameFromHostNameHeaderInitializer
        {
            private readonly HttpContext fakeContext;
            private Func<HttpContext> resolvePlatformContext = null;
            private Func<HttpContext, RequestTelemetry> getRequestFromContext = null;

            public TestableAzureAppServiceRoleNameFromHostNameHeaderInitializer(IDictionary<string, string> headers = null,
                Func<HttpContext> resolveContext = null,
                Func<HttpContext, RequestTelemetry> getRequestFromContext = null)
            {
                this.fakeContext = HttpModuleHelper.GetFakeHttpContext(headers);
                this.resolvePlatformContext = resolveContext;
                this.getRequestFromContext = getRequestFromContext;
            }

            public TestableAzureAppServiceRoleNameFromHostNameHeaderInitializer(string webAppSuffix) : base(webAppSuffix)
            {
            }

            protected override HttpContext ResolvePlatformContext()
            {
                if (this.resolvePlatformContext != null)
                {
                    return this.resolvePlatformContext();
                }
                else
                {
                    return this.fakeContext;
                }
            }

            protected override RequestTelemetry GetRequestFromContext(HttpContext ctx)
            {
                if (this.getRequestFromContext != null)
                {
                    return this.getRequestFromContext(ctx);
                }
                else
                {
                    return null;
                }
            }
        }
    }
}
