using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights.Extensibility;
using Microsoft.ApplicationInsights.Extensibility.EventCounterCollector;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DemoEventCounterPerfCounter
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();
            services.AddWebEncoders();
            services.AddApplicationInsightsTelemetry("abeb8110-3909-4ca6-9878-3e2b19a50005");

            services.AddSingleton<ITelemetryModule, EventCounterCollectionModule>();
            services.ConfigureTelemetryModule<EventCounterCollectionModule>((eventCounterModule, options) =>
            {
                eventCounterModule.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "cpu-usage"));
                eventCounterModule.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "working-set"));
                eventCounterModule.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "gc-heap-size"));
                eventCounterModule.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "gen-0-gc-count"));
                eventCounterModule.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "gen-1-gc-count"));
                eventCounterModule.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "gen-2-gc-count"));
                eventCounterModule.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "time-in-gc"));
                eventCounterModule.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "gen-0-size"));
                eventCounterModule.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "gen-1-size"));
                eventCounterModule.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "gen-2-size"));
                eventCounterModule.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "loh-size"));
                eventCounterModule.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "alloc-rate"));
                eventCounterModule.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "assembly-count"));
                eventCounterModule.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "exception-count"));
                eventCounterModule.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "threadpool-thread-count"));
                eventCounterModule.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "monitor-lock-contention-count"));
                eventCounterModule.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "threadpool-queue-length"));
                eventCounterModule.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "threadpool-completed-items-count"));
                eventCounterModule.Counters.Add(new EventCounterCollectionRequest("System.Runtime", "active-timer-count"));
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
