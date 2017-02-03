// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;
using Microsoft.Diagnostics.Context;
using Microsoft.Diagnostics.Correlation.Common;
using Microsoft.Diagnostics.Correlation.Common.Instrumentation;

namespace Microsoft.Diagnostics.Correlation.AspNetCore.Internal
{
    internal class HttpDiagnosticListenerObserver<TContext> : IObserver<KeyValuePair<string, object>> where TContext : ICorrelationContext<TContext>
    {
        private readonly List<IContextInjector<TContext, HttpRequestMessage>> contextInjectors;
        private readonly IEndpointFilter endpointFilter;
        private readonly IOutgoingRequestNotifier<TContext, HttpRequestMessage, HttpResponseMessage> requestNotifier;

        public HttpDiagnosticListenerObserver(
            IEnumerable<IContextInjector<TContext, HttpRequestMessage>> contextInjectors,
            IEndpointFilter endpointFilter,
            IOutgoingRequestNotifier<TContext, HttpRequestMessage, HttpResponseMessage> requestNotifier)
        {
            this.endpointFilter = endpointFilter;
            this.contextInjectors = new List<IContextInjector<TContext, HttpRequestMessage>>(contextInjectors);
            this.requestNotifier = requestNotifier;
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            if (value.Value == null)
                return;

            if (value.Key == "System.Net.Http.Request")
            {
                PropertyInfo requestInfo = value.Value.GetType().GetProperty("Request");
                HttpRequestMessage request = (HttpRequestMessage)requestInfo?.GetValue(value.Value, null);

                if (request != null)
                {
                    var ctx = ContextResolver.GetContext<TContext>();
                    if (endpointFilter.Validate(request.RequestUri))
                    {
                        foreach (var injector in contextInjectors)
                        {
                            injector.UpdateRequest(ctx, request);
                        }
                        requestNotifier.OnBeforeRequest(ctx, request);
                    }
                }
            }
            else if (value.Key == "System.Net.Http.Response")
            {
                var responseInfo = value.Value.GetType().GetProperty("Response");
                var response = (HttpResponseMessage)responseInfo?.GetValue(value.Value, null);
                if (response != null)
                {
                    if (endpointFilter.Validate(response.RequestMessage.RequestUri))
                    {
                        requestNotifier.OnAfterResponse(ContextResolver.GetContext<TContext>(), response);
                    }
                }
            }
        }

        public void OnCompleted() { }

        public void OnError(Exception error) { }
    }
}