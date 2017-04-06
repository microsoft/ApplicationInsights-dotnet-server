namespace Microsoft.ApplicationInsights.DependencyCollector.Implementation.HttpParsers
{
    using System;
    using System.Collections.Generic;

    using DataContracts;

    /// <summary>
    /// HTTP Dependency parser that attempts to parse dependency as Azure DocumentDB call.
    /// </summary>
    internal static class DocumentDbHttpParser
    {
        private const string CreateOrQueryDocumentOperationName = "Create/Query Document";

        private static readonly string[] DocumentDbVerbPrefixes = { "GET ", "POST ", "PUT ", "HEAD ", "DELETE " };

        private static readonly Dictionary<string, string> OperationNames = new Dictionary<string, string>
        {
            // Database operations
            ["POST dbs"] = "Create Database",
            ["GET dbs"] = "List Databases",
            ["GET dbs/*"] = "Get Database",
            ["DELETE dbs/*"] = "Delete Database",
            // Collection operations
            ["POST dbs/*/colls"] = "Create Collection",
            ["GET dbs/*/colls"] = "List Collections",
            ["GET dbs/*/colls/*"] = "Get Collection",
            ["DELETE dbs/*/colls/*"] = "Delete Collection",
            ["PUT dbs/*/colls/*"] = "Replace Collection",
            // Document operations
            ["POST dbs/*/colls/*/docs"] = CreateOrQueryDocumentOperationName, // Create & Query share this moniker
            ["GET dbs/*/colls/*/docs"] = "List Documents",
            ["GET dbs/*/colls/*/docs/*"] = "Get Document",
            ["DELETE dbs/*/colls/*/docs/*"] = "Delete Document",
            ["PUT dbs/*/colls/*/docs/*"] = "Replace Document",
            // Attachment operations
            ["POST dbs/*/colls/*/docs/*/attachments"] = "Create Attachment",
            ["GET dbs/*/colls/*/docs/*/attachments"] = "List Attachments",
            ["GET dbs/*/colls/*/docs/*/attachments/*"] = "Get Attachment",
            ["DELETE dbs/*/colls/*/docs/*/attachments/*"] = "Delete Attachment",
            ["PUT dbs/*/colls/*/docs/*/attachments/*"] = "Replace Attachment",
            // Stored procedure operations
            ["POST dbs/*/colls/*/sprocs"] = "Create Stored Procedure",
            ["PUT dbs/*/colls/*/sprocs/*"] = "Replace Stored Procedure",
            ["GET dbs/*/colls/*/sprocs"] = "List Stored Procedures",
            ["DELETE dbs/*/colls/*/sprocs/*"] = "Delete Stored Procedure",
            ["POST dbs/*/colls/*/sprocs/*"] = "Execute Stored Procedure",
            // User defined function operations
            ["POST dbs/*/colls/*/udfs"] = "Create UDF",
            ["PUT dbs/*/colls/*/udfs/*"] = "Replace UDF",
            ["GET dbs/*/colls/*/udfs"] = "List UDFs",
            ["DELETE dbs/*/colls/*/udfs/*"] = "Delete UDF",
            // Trigger operations
            ["POST dbs/*/colls/*/triggers"] = "Create Trigger",
            ["PUT dbs/*/colls/*/triggers/*"] = "Replace Trigger",
            ["GET dbs/*/colls/*/triggers"] = "List Triggers",
            ["DELETE dbs/*/colls/*/triggers/*"] = "Delete Trigger",
            // User operations
            ["POST dbs/*/users"] = "Create User",
            ["GET dbs/*/users"] = "List Users",
            ["GET dbs/*/users/*"] = "Get User",
            ["DELETE dbs/*/users/*"] = "Delete User",
            ["PUT dbs/*/users/*"] = "Replace User",
            // Permission operations
            ["POST dbs/*/users/*/permissions"] = "Create Permission",
            ["GET dbs/*/users/*/permissions"] = "List Permission",
            ["GET dbs/*/users/*/permissions/*"] = "Get Permission",
            ["DELETE dbs/*/users/*/permissions/*"] = "Delete Permission",
            ["PUT dbs/*/users/*/permissions/*"] = "Replace Permission",
            // Offer operations
            ["POST offers"] = "Query Offers",
            ["GET offers"] = "List Offers",
            ["GET offers/*"] = "Get Offer",
            ["PUT offers/*"] = "Replace Offer"
        };

        /// <summary>
        /// Tries parsing given dependency telemetry item. 
        /// </summary>
        /// <param name="httpDependency">Dependency item to parse. It is expected to be of HTTP type.</param>
        /// <returns><code>true</code> if successfully parsed dependency.</returns>
        internal static bool TryParse(ref DependencyTelemetry httpDependency)
        {
            string name = httpDependency.Name;
            string host = httpDependency.Target;
            string url = httpDependency.Data;

            if (name == null || host == null || url == null)
            {
                return false;
            }

            if (!host.EndsWith(".documents.azure.com", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            ////
            //// DocumentDB REST API: https://docs.microsoft.com/en-us/rest/api/documentdb/
            ////

            string account = host.Substring(0, host.IndexOf('.'));

            string verb = null;
            string nameWithoutVerb = name;

            for (int i = 0; i < DocumentDbVerbPrefixes.Length; i++)
            {
                var verbPrefix = DocumentDbVerbPrefixes[i];
                if (name.StartsWith(verbPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    verb = name.Substring(0, verbPrefix.Length - 1);
                    nameWithoutVerb = name.Substring(verbPrefix.Length);
                    break;
                }
            }

            List<KeyValuePair<string, string>> resourcePath = HttpParsingHelper.ParseResourcePath(nameWithoutVerb);

            foreach (var resource in resourcePath)
            {
                if (resource.Value != null)
                {
                    string propertyName = GetPropertyNameForResource(resource.Key);
                    if (propertyName != null)
                    {
                        httpDependency.Properties[propertyName] = resource.Value;
                    }
                }
            }

            string operation = HttpParsingHelper.BuildOperationMoniker(verb, resourcePath);
            string operationName = GetOperationName(httpDependency, operation);

            httpDependency.Type = RemoteDependencyConstants.AzureDocumentDb;
            httpDependency.Name = operationName;

            return true;
        }

        private static string GetPropertyNameForResource(string resourceType)
        {
            switch (resourceType)
            {
                case "dbs":
                    return "Database";
                case "colls":
                    return "Collection";
                case "sprocs":
                    return "Stored procedure";
                case "udfs":
                    return "User defined function";
                case "triggers":
                    return "Trigger";
                default:
                    return null;
            }
        }

        private static string GetOperationName(DependencyTelemetry httpDependency, string operation)
        {
            string operationName;
            if (!OperationNames.TryGetValue(operation, out operationName))
            {
                return operationName;
            }

            // "Create Document" and "Query Document" share the same moniker
            // but we can try to distinguish them by response code
            if (operationName == CreateOrQueryDocumentOperationName)
            {
                switch (httpDependency.ResultCode)
                {
                    case "200":
                        {
                            operationName = "Query Document";
                            break;
                        }
                    case "201":
                    case "403":
                    case "409":
                    case "413":
                        {
                            operationName = "Create Document";
                            break;
                        }
                    default:
                        // keep the ambiguous name
                        break;
                }
            }

            return operationName;
        }
    }
}
