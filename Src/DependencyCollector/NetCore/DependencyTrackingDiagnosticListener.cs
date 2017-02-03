// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Reflection;

namespace Microsoft.Diagnostics.Correlation.AspNetCore.Internal
{
    internal class DependencyTrackingDiagnosticListener : IObserver<KeyValuePair<string, object>>
    {
        public DependencyTrackingDiagnosticListener()
        {
        }

        public void OnNext(KeyValuePair<string, object> value)
        {
            if (value.Value == null)
                return;

            if (value.Key == "System.Net.Http.Request")
            {
                PropertyInfo requestInfo = value.Value.GetType().GetRuntimeProperty("Request");
                HttpRequestMessage request = (HttpRequestMessage)requestInfo?.GetValue(value.Value, null);

                if (request != null)
                {
                }
            }
            else if (value.Key == "System.Net.Http.Response")
            {
                PropertyInfo responseInfo = value.Value.GetType().GetRuntimeProperty("Response");
                HttpResponseMessage response = (HttpResponseMessage)responseInfo?.GetValue(value.Value, null);
                if (response != null)
                {
                }
            }
        }

        public void OnCompleted() { }

        public void OnError(Exception error) { }
    }
}