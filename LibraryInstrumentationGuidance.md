# Guidance for instrumenting libraries with Diagnostic Source 

This document provides guidance for adding diagnostic instrumentation to the library, which allows any monitoring solutions to collect meaningful and rich telemetry inside the process.

# Instrumentation principles

* **No instrumentation in user application**. When tracing system is injected (at runtime or compile-time), it automatically discovers all diagnostic sources in the app and may subscribe to any of them. 
* **Minimal performance impact**. In absence of listener, instrumentation has neglectable impact on the performance. When listener is present, it is able to sample events to minimize the reasonable impact instrumentation may have.
* **Baked into the platform**. To make sure everything could be instrumented (e.g. .NET HttpClient or SqlClient), instrumentation primitives are part of .NET
* **General purpose**. Could be consumed by any tracing/monitoring/profiling tool and the tool can choose level of details it gives its users

## Diagnostic Source and Activities

[Diagnostic Source][DiagnosticSourceGuide] is a simple module that allows code to be instrumented for production-time logging of rich data payloads for consumption within the process that was instrumented. At runtime, consumers can dynamically discover data sources and subscribe to the ones of interest.

[Activity][ActivityGuide] is a class that allows storing and accessing diagnostics context and consuming it with logging system. Activity flows with async calls and anything can request Activity.Current and correlate the trace/log with it.

Both Diagnostic Source and Activity have been used to instrument [System.Net.Http][SystemNetHttp] and [Microsoft.AspNetCore.Hosting][MicrosoftAspNetCoreHosting].
More recently two new libraries were instrumented and that work was the basis for this guidance. These libraries are client SDKs for [Azure Event Hubs][MicrosoftEventHubs] and [Azure Service Bus][MicrosoftServiceBus], both of which support high throughput scenarios.

## What should be instrumented

The goal of instrumentation is to give the users the visibility to how particular operations are being performed inside the library. This information can be later used to diagnose performance or issues. It is up to the library authors to identify operations that the library performs and are worthy of monitoring. These operations can match the exposed API but also cover more specific internal logic (like outgoing service calls, retries, locking, cache utilization, awaiting system events, etc.). This way the users can get a good understanding of what's going on under the hood when they need it. 

## Bare Minimum Instrumentation 

In the simplest case, the operation that is being monitored has to be wrapped by an activity. 

```csharp
    // create DiagnosticListener with unique name
    static DiagnosticListener source = new DiagnosticListener("Example.MyLibrary");

    Activity activity = null;
    
    // check if listener is interested in this operation
    if (source.IsEnabled() && source.IsEnabled("MyOperation"))
    {
        activity = new Activity("MyOperation");
        activity.Start();
    }

    object output = null;
    try
    {
        // perform the actual operation
        output = RunOperation(input);
    }
    catch (Exception ex)
    {
        activity?.AddTag("error", true);
    }
    finally
    {
        // stop activity if started
        if (activity != null)
             source.StopActivity(activity, new MyStopPayload { Input = input, Output = output }); 
    }
```
> ### *__TODO__ - provide a pointer to a document with more advanced instrumentation examples (WIP)* 

This minimum instrumentation give tracing system a chance to trace something like '<timestamp> [Example.MyLibrary] MyOperation, Id 'abc.1.2.3.', took <duration> ms, with properties [{ "error": true }]'.
While in majority of cases some other details are desirable (and we will get to it [here](TBD)), this instrumentation gives bare minimum.

### Instrumentation control and sampling considerations

**DO** make `DiagnosticListener` name globally unique.
Every DiagnosticListener has a name. Tracing system discovers all available sources and subscribes to them based on the name. 
[DiagnosticSource guide](https://github.com/dotnet/corefx/blob/master/src/System.Diagnostics.DiagnosticSource/src/DiagnosticSourceUsersGuide.md#naming-conventions) provides more naming considerations.

**DO** call 'IsEnabled()' for every operation.
The first `source.IsEnabled()` call efficiently tell if there is any listener in the system for our source. 

**DO** make operation name unique within the DiagnosticListener. Operation name is coarse name that remains the same for ALL operations of this kind (e.g. `HttpRequestIn` or `HttpRequestOut`)

**DO** call 'IsEnabled(OperationName)' for every operation
If there is a listener, we ask tracing system if it is interested in 'MyOperation' with `source.IsEnabled("MyOperation")`. It enables filtering based on the operation name, but more importantly gives a tracing system a chance to sample this trace out based on the current Activity: tracing system samples events consistently to trace all or nothing for high-level operation. 

**CONSIDER** providing more context via 'IsEnabled(OperationName, Input)' overload instaed of 'IsEnabled(OperationName)'
In some cases tracing system needs more context to filter operations. A good example is tracing system that tracks HTTP calls and sends telemetry over HTTP as well. To avoid recursive tracing and save performance tracing system want to prevent such calls to be instrumented at all based on some request properties.

**CONSIDER** firing 'MyOperation.Start' event
In many cases it makes sense to send Start event to tracing system. It may use it to inject some context into the `Input`. However tracing system may not be interested in such event at all, so guard 'Start' event with `IsEnabled` cliche, 
but make sure `Activity` is started in any case:


```csharp
    if (source.IsEnabled("MyOperation.Start"))
        source.StartActivity(activity, new MyStartPayload {Input = input});
    else
        activity.Start();
```

DO NOT cache results of IsEnabled: tracing system may subscribe/unsubscribe at any moment. Results of IsEnabled are dynamic and depend on the context (`Activity.Current`, `Input`).

### Context Propagation

In distributed systems, one user operation may be processed by many applications. To trace operation end-to-end, some context must be propagated along with external call.

#### Injection (outgoing calls)

Library should inject the context into the wired protocols if there is a standard defined for this protocol and library implements this protocol and let tracing system add/change correlation context.

* DO fire 'MyOperation.Start' (guarded by IsEnabled) event if your operation involves out-of-process process call. Provide raw request (`Input`) in the event payload so tracing system could inject context.
* DO include raw response into the 'MyOperation.Stop' event payload (`Output`): tracing system may want to access response and parse context that back propagated back in response.
* DO propagate context if the standard for the propagation exists. Some servers (e.g. SQL) could support correlating internal logs with external operations and define metadata fields for correlation identifiers, however may require specific identifier(s) format. Use `activity.Id` if possible, otherwise generate a compatible id and add a tag: `activity.AddTag("sqlCorrelationId", myGeneratedId)`
* DO NOT propagate context if your library only uses HTTP protocol. .NET handles HTTP layer, and tracing system may alter it.
* CONSIDER If the standard does not exists, but you see a value in propagating context for correlation, use the names and format suggested by the [W3C distributed tracing](https://w3c.github.io/distributed-tracing). Use [TBD] field in `tracestate` for `activity.Id`.

#### Extraction (incoming calls)

TODO

### Payload

When starting and stopping an activity, it is possible to pass a payload object, which will be available for the listener. This way the listener can access raw, non-serialized data in real time. It can be useful for pulling out additional diagnostic information but also manipulating data as it is being processed (for example, inject diagnostic context before an outbound call is made). Good examples of such payload are ```HttpRequestMessage``` or messages that are being passed through queues.

Mind that payload is not preserved as part of Activity and is only available when activity is started/stopped. Therefore it is a good practice to specify all data that was passed to ```StartActivity()``` in ```StopActivity()``` as well.

#### Payload format

TODO: 
DO Use public concrete types. 
DO Use PascalCase for properties
DO ensure back-compat, consider it as public API area
Remove defined payload properties and define tags
Describe payload usage scenarios, 
How to efficiently parse payloads for anonymous types

Diagnostic source event and activity start/stop API allows to specify only a single payload object. However, in order to pass more data and allow future additions the recommendation is to use dynamic objects. Since these are .NET objects the names of particular properties should follow [standard .NET naming convention][DotNetPropertyNamingConvention]. 

Here are some recommendations for typical payload property names:

| Property name | Description |
|:--------------|:-------------------|
| `Endpoint` | The ```Uri``` of an endpoint the activity is for (for example, target database, service) |
| `PartitionKey` | The key/ID of the partition the activity is for |
| `Status` | The ```TaskStatus``` of a completed asynchronous task |
| `Exception` | The captured exception object |

### Tags

TODO: describe tags-only scenarios

Activities can have additional tracing information in tags. Tags are meant to be easily, efficiently consumable and are expected to be logged with the activity without any processing. As such they should only contain essential information that should be made available to users to determine if the activity is of interest to them. All of the rich details should be made available in the payload.

Tags can be added to activity at any time of its existence until it is stopped. This way the activity can be enriched with information from operation input, output and/or exceptions. Note however that they should be specified as early as possible so that the listeners can have the most context. In particular, all available tags should be set before starting the activity. 

Tags are not propagated to child activities.

```csharp
    activity.AddTag("size", "small");
    activity.AddTag("color", "blue");
```

#### Tag naming

A single application can include numerous libraries instrumented with Diagnostic Source. In order to maintain a certain level of consistency of the whole diagnostic data, it is recommended to use a common tag naming convention. [OpenTracing][OpenTracingNamingConvention] published naming convention for such tags and can be used as a reference. If no suitable tag was defined there, then a new name can be used, ideally following the same convention.  



[DiagnosticSourceGuide]: https://github.com/dotnet/corefx/blob/master/src/System.Diagnostics.DiagnosticSource/src/DiagnosticSourceUsersGuide.md
[ActivityGuide]: https://github.com/dotnet/corefx/blob/master/src/System.Diagnostics.DiagnosticSource/src/ActivityUserGuide.md
[OpenTracingNamingConvention]: https://github.com/opentracing/specification/blob/master/semantic_conventions.md#span-tags-table
[SystemNetHttp]: https://github.com/dotnet/corefx/blob/master/src/System.Net.Http/src/System/Net/Http/DiagnosticsHandler.cs
[MicrosoftAspNetCoreHosting]: https://github.com/aspnet/Hosting/blob/dev/src/Microsoft.AspNetCore.Hosting/Internal/HostingApplicationDiagnostics.cs
[MicrosoftEventHubs]: https://github.com/Azure/azure-event-hubs-dotnet/blob/dev/src/Microsoft.Azure.EventHubs/EventHubsDiagnosticSource.cs
[MicrosoftServiceBus]: https://github.com/Azure/azure-service-bus-dotnet/blob/dev/src/Microsoft.Azure.ServiceBus/ServiceBusDiagnosticsSource.cs
[DotNetPropertyNamingConvention]: https://docs.microsoft.com/en-us/dotnet/standard/design-guidelines/names-of-type-members#names-of-properties