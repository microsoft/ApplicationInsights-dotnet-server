namespace Microsoft.ApplicationInsights.DependencyCollector.Implementation.SqlClientDiagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.Diagnostics;
    using System.Globalization;

    using Microsoft.ApplicationInsights.Common;
    using Microsoft.ApplicationInsights.DataContracts;
    using Microsoft.ApplicationInsights.DependencyCollector.Implementation.Operation;
    using Microsoft.ApplicationInsights.Extensibility;
    using Microsoft.ApplicationInsights.Extensibility.Implementation;
    using Microsoft.ApplicationInsights.Extensibility.Implementation.Tracing;
    using static Microsoft.ApplicationInsights.DependencyCollector.Implementation.SqlClientDiagnostics.SqlClientDiagnosticFetcherTypes;

    internal class SqlClientDiagnosticSourceListener : IObserver<KeyValuePair<string, object>>, IDisposable
    {
        // Event ids defined at: https://github.com/dotnet/corefx/blob/master/src/System.Data.SqlClient/src/System/Data/SqlClient/SqlClientDiagnosticListenerExtensions.cs
        public const string DiagnosticListenerName = "SqlClientDiagnosticListener";

        public const string SqlBeforeExecuteCommand = SqlClientPrefix + "WriteCommandBefore";
        public const string SqlMicrosoftBeforeExecuteCommand = SqlMicrosoftClientPrefix + "WriteCommandBefore";

        public const string SqlAfterExecuteCommand = SqlClientPrefix + "WriteCommandAfter";
        public const string SqlMicrosoftAfterExecuteCommand = SqlMicrosoftClientPrefix + "WriteCommandAfter";

        public const string SqlErrorExecuteCommand = SqlClientPrefix + "WriteCommandError";
        public const string SqlMicrosoftErrorExecuteCommand = SqlMicrosoftClientPrefix + "WriteCommandError";

        public const string SqlBeforeOpenConnection = SqlClientPrefix + "WriteConnectionOpenBefore";
        public const string SqlMicrosoftBeforeOpenConnection = SqlMicrosoftClientPrefix + "WriteConnectionOpenBefore";

        public const string SqlAfterOpenConnection = SqlClientPrefix + "WriteConnectionOpenAfter";
        public const string SqlMicrosoftAfterOpenConnection = SqlMicrosoftClientPrefix + "WriteConnectionOpenAfter";
        
        public const string SqlErrorOpenConnection = SqlClientPrefix + "WriteConnectionOpenError";
        public const string SqlMicrosoftErrorOpenConnection = SqlMicrosoftClientPrefix + "WriteConnectionOpenError";

        public const string SqlBeforeCloseConnection = SqlClientPrefix + "WriteConnectionCloseBefore";
        public const string SqlMicrosoftBeforeCloseConnection = SqlMicrosoftClientPrefix + "WriteConnectionCloseBefore";

        public const string SqlAfterCloseConnection = SqlClientPrefix + "WriteConnectionCloseAfter";
        public const string SqlMicrosoftAfterCloseConnection = SqlMicrosoftClientPrefix + "WriteConnectionCloseAfter";

        public const string SqlErrorCloseConnection = SqlClientPrefix + "WriteConnectionCloseError";
        public const string SqlMicrosoftErrorCloseConnection = SqlMicrosoftClientPrefix + "WriteConnectionCloseError";

        public const string SqlBeforeCommitTransaction = SqlClientPrefix + "WriteTransactionCommitBefore";
        public const string SqlMicrosoftBeforeCommitTransaction = SqlMicrosoftClientPrefix + "WriteTransactionCommitBefore";

        public const string SqlAfterCommitTransaction = SqlClientPrefix + "WriteTransactionCommitAfter";
        public const string SqlMicrosoftAfterCommitTransaction = SqlMicrosoftClientPrefix + "WriteTransactionCommitAfter";
        
        public const string SqlErrorCommitTransaction = SqlClientPrefix + "WriteTransactionCommitError";
        public const string SqlMicrosoftErrorCommitTransaction = SqlMicrosoftClientPrefix + "WriteTransactionCommitError";

        public const string SqlBeforeRollbackTransaction = SqlClientPrefix + "WriteTransactionRollbackBefore";
        public const string SqlMicrosoftBeforeRollbackTransaction = SqlMicrosoftClientPrefix + "WriteTransactionRollbackBefore";

        public const string SqlAfterRollbackTransaction = SqlClientPrefix + "WriteTransactionRollbackAfter";
        public const string SqlMicrosoftAfterRollbackTransaction = SqlMicrosoftClientPrefix + "WriteTransactionRollbackAfter";

        public const string SqlErrorRollbackTransaction = SqlClientPrefix + "WriteTransactionRollbackError";
        public const string SqlMicrosoftErrorRollbackTransaction = SqlMicrosoftClientPrefix + "WriteTransactionRollbackError";

        private const string SqlClientPrefix = "System.Data.SqlClient.";
        private const string SqlMicrosoftClientPrefix = "Microsoft.Data.SqlClient.";

        private static readonly ActiveSubsciptionManager SubscriptionManager = new ActiveSubsciptionManager();
        private readonly TelemetryClient client;
        private readonly SqlClientDiagnosticSourceSubscriber subscriber;

        private readonly ObjectInstanceBasedOperationHolder operationHolder = new ObjectInstanceBasedOperationHolder();

        public SqlClientDiagnosticSourceListener(TelemetryConfiguration configuration)
        {
            this.client = new TelemetryClient(configuration);
            this.client.Context.GetInternalContext().SdkVersion =
                SdkVersionUtils.GetSdkVersion("rdd" + RddSource.DiagnosticSourceCore + ":");

            this.subscriber = new SqlClientDiagnosticSourceSubscriber(this);
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        void IObserver<KeyValuePair<string, object>>.OnCompleted()
        {
        }

        void IObserver<KeyValuePair<string, object>>.OnError(Exception error)
        {
        }

        void IObserver<KeyValuePair<string, object>>.OnNext(KeyValuePair<string, object> evnt)
        {
            try
            {
                // It's possible to host multiple apps (ASP.NET Core or generic hosts) in the same process
                // Each of this apps has it's own DependencyTrackingModule and corresponding SQL listener.
                // We should ignore events for all of them except one
                if (!SubscriptionManager.IsActive(this))
                {
                    DependencyCollectorEventSource.Log.NotActiveListenerNoTracking(evnt.Key, Activity.Current?.Id);
                    return;
                }

                switch (evnt.Key)
                {
                    case SqlBeforeExecuteCommand:
                    case SqlMicrosoftBeforeExecuteCommand:
                    {
                        var operationId = (Guid)CommandBefore.OperationId.Fetch(evnt.Value);
                        DependencyCollectorEventSource.Log.SqlClientDiagnosticSubscriberCallbackCalled(operationId, evnt.Key);

                        var command = CommandBefore.Command.Fetch(evnt.Value);

                        if (this.operationHolder.Get(command) == null)
                        {
                            var dependencyName = string.Empty;
                            var target = string.Empty;

                            var commandText = (string)CommandBefore.CommandText.Fetch(command);
                            var con = CommandBefore.Connection.Fetch(command);
                            if (con != null)
                            {
                                var dataSource = CommandBefore.DataSource.Fetch(con);
                                var database = CommandBefore.Database.Fetch(con);
                                target = string.Join(" | ", dataSource, database);
                                
                                var commandName = (CommandType)CommandBefore.CommandType.Fetch(command) == CommandType.StoredProcedure
                                    ? commandText
                                    : string.Empty;

                                dependencyName = string.IsNullOrEmpty(commandName)
                                    ? string.Join(" | ", dataSource, database)
                                    : string.Join(" | ", dataSource, database, commandName);
                            }

                            var timestamp = CommandBefore.Timestamp.Fetch(evnt.Value) as long?
                                        ?? Stopwatch.GetTimestamp(); // TODO corefx#20748 - timestamp missing from event data

                            var telemetry = new DependencyTelemetry()
                            {
                                Id = operationId.ToStringInvariant("N"),
                                Name = dependencyName,
                                Type = RemoteDependencyConstants.SQL,
                                Target = target,
                                Data = commandText,
                                Success = true,
                            }; 

                            // Populate the operation details for initializers
                            telemetry.SetOperationDetail(RemoteDependencyConstants.SqlCommandOperationDetailName, command);

                            InitializeTelemetry(telemetry, operationId, timestamp);

                            this.operationHolder.Store(command, Tuple.Create(telemetry, /* isCustomCreated: */ false));
                        }
                        else
                        {
                            DependencyCollectorEventSource.Log.TrackingAnExistingTelemetryItemVerbose();
                        }

                        break;
                    }

                    case SqlAfterExecuteCommand:
                    case SqlMicrosoftAfterExecuteCommand:
                    {
                        var operationId = (Guid)CommandAfter.OperationId.Fetch(evnt.Value);

                        DependencyCollectorEventSource.Log.SqlClientDiagnosticSubscriberCallbackCalled(operationId, evnt.Key);

                        var command = CommandAfter.Command.Fetch(evnt.Value);
                        var tuple = this.operationHolder.Get(command);

                        if (tuple != null)
                        {
                            this.operationHolder.Remove(command);

                            var telemetry = tuple.Item1;

                            var timestamp = (long)CommandAfter.Timestamp.Fetch(evnt.Value);

                            telemetry.Stop(timestamp);

                            this.client.TrackDependency(telemetry);
                        }
                        else
                        {
                            DependencyCollectorEventSource.Log.EndCallbackWithNoBegin(operationId.ToStringInvariant("N"));
                        }

                        break;
                    }

                    case SqlErrorExecuteCommand:
                    case SqlMicrosoftErrorExecuteCommand:
                    {
                        var operationId = (Guid)CommandError.OperationId.Fetch(evnt.Value);

                        DependencyCollectorEventSource.Log.SqlClientDiagnosticSubscriberCallbackCalled(operationId, evnt.Key);

                        var command = CommandError.Command.Fetch(evnt.Value);
                        var tuple = this.operationHolder.Get(command);

                        if (tuple != null)
                        {
                            this.operationHolder.Remove(command);

                            var telemetry = tuple.Item1;

                            var timestamp = (long)CommandError.Timestamp.Fetch(evnt.Value);

                            telemetry.Stop(timestamp);

                            var exception = (Exception)CommandError.Exception.Fetch(evnt.Value);

                            ConfigureExceptionTelemetry(telemetry, exception);

                            this.client.TrackDependency(telemetry);
                        }
                        else
                        {
                            DependencyCollectorEventSource.Log.EndCallbackWithNoBegin(operationId.ToStringInvariant("N"));
                        }

                        break;
                    }
                    
                    case SqlBeforeOpenConnection:
                    case SqlMicrosoftBeforeOpenConnection:
                    {
                        var operationId = (Guid)ConnectionBefore.OperationId.Fetch(evnt.Value);

                        DependencyCollectorEventSource.Log.SqlClientDiagnosticSubscriberCallbackCalled(operationId, evnt.Key);
                    
                        var connection = ConnectionBefore.Connection.Fetch(evnt.Value);

                        if (this.operationHolder.Get(connection) == null)
                        {
                            var operation = (string)ConnectionBefore.Operation.Fetch(evnt.Value);
                            var timestamp = (long)ConnectionBefore.Timestamp.Fetch(evnt.Value);
                            var dataSource = (string)ConnectionBefore.DataSource.Fetch(connection);
                            var database = (string)ConnectionBefore.Database.Fetch(connection);
                            var telemetry = new DependencyTelemetry()
                            {
                                Id = operationId.ToStringInvariant("N"),
                                Name = string.Join(" | ", dataSource, database, operation),
                                Type = RemoteDependencyConstants.SQL,
                                Target = string.Join(" | ", dataSource, database),
                                Data = operation,
                                Success = true,
                            };

                            InitializeTelemetry(telemetry, operationId, timestamp);

                            this.operationHolder.Store(connection, Tuple.Create(telemetry, /* isCustomCreated: */ false));
                        }
                        else
                        {
                            DependencyCollectorEventSource.Log.TrackingAnExistingTelemetryItemVerbose();
                        }

                        break;
                    }

                    case SqlAfterOpenConnection:
                    case SqlMicrosoftAfterOpenConnection:
                    {
                        var operationId = (Guid)ConnectionAfter.OperationId.Fetch(evnt.Value);

                        DependencyCollectorEventSource.Log.SqlClientDiagnosticSubscriberCallbackCalled(operationId, evnt.Key);

                        var connection = ConnectionAfter.Connection.Fetch(evnt.Value);
                        var tuple = this.operationHolder.Get(connection);

                        if (tuple != null)
                        {
                            this.operationHolder.Remove(connection);
                        }
                        else
                        {
                            DependencyCollectorEventSource.Log.EndCallbackWithNoBegin(operationId.ToStringInvariant("N"));
                        }

                        break;
                    }

                    case SqlErrorOpenConnection:
                    case SqlMicrosoftErrorOpenConnection:
                    {
                        var operationId = (Guid)ConnectionError.OperationId.Fetch(evnt.Value);

                        DependencyCollectorEventSource.Log.SqlClientDiagnosticSubscriberCallbackCalled(operationId, evnt.Key);

                        var connection = ConnectionError.Connection.Fetch(evnt.Value);
                        var tuple = this.operationHolder.Get(connection);

                        if (tuple != null)
                        {
                            this.operationHolder.Remove(connection);

                            var telemetry = tuple.Item1;

                            var timestamp = (long)ConnectionError.Timestamp.Fetch(evnt.Value);

                            telemetry.Stop(timestamp);

                            var exception = (Exception)ConnectionError.Exception.Fetch(evnt.Value);

                            ConfigureExceptionTelemetry(telemetry, exception);

                            this.client.TrackDependency(telemetry);
                        }
                        else
                        {
                            DependencyCollectorEventSource.Log.EndCallbackWithNoBegin(operationId.ToStringInvariant("N"));
                        }

                        break;
                    }

                    case SqlBeforeCommitTransaction:
                    case SqlMicrosoftBeforeCommitTransaction:
                    {
                        var operationId = (Guid)TransactionCommitBefore.OperationId.Fetch(evnt.Value);

                        DependencyCollectorEventSource.Log.SqlClientDiagnosticSubscriberCallbackCalled(operationId, evnt.Key);

                        var connection = (SqlConnection)TransactionCommitBefore.Connection.Fetch(evnt.Value);

                        if (this.operationHolder.Get(connection) == null)
                        {
                            var operation = (string)TransactionCommitBefore.Operation.Fetch(evnt.Value);
                            var timestamp = (long)TransactionCommitBefore.Timestamp.Fetch(evnt.Value);
                            var isolationLevel = (IsolationLevel)TransactionCommitBefore.IsolationLevel.Fetch(evnt.Value);
                            var dataSource = (string)TransactionCommitBefore.DataSource.Fetch(connection);
                            var database = (string)TransactionCommitBefore.Database.Fetch(connection);

                                var telemetry = new DependencyTelemetry()
                            {
                                Id = operationId.ToStringInvariant("N"),
                                Name = string.Join(" | ", dataSource, database, operation, isolationLevel),
                                Type = RemoteDependencyConstants.SQL,
                                Target = string.Join(" | ", dataSource, database),
                                Data = operation,
                                Success = true,
                            };

                            InitializeTelemetry(telemetry, operationId, timestamp);

                            this.operationHolder.Store(connection, Tuple.Create(telemetry, /* isCustomCreated: */ false));
                        }
                        else
                        {
                            DependencyCollectorEventSource.Log.TrackingAnExistingTelemetryItemVerbose();
                        }

                        break;
                    }

                    case SqlBeforeRollbackTransaction:
                    case SqlMicrosoftBeforeRollbackTransaction:
                    {
                        var operationId = (Guid)TransactionRollbackBefore.OperationId.Fetch(evnt.Value);

                        DependencyCollectorEventSource.Log.SqlClientDiagnosticSubscriberCallbackCalled(operationId, evnt.Key);

                        var connection = TransactionRollbackBefore.Connection.Fetch(evnt.Value);

                        if (this.operationHolder.Get(connection) == null)
                        {
                            var operation = (string)TransactionRollbackBefore.Operation.Fetch(evnt.Value);
                            var timestamp = (long)TransactionRollbackBefore.Timestamp.Fetch(evnt.Value);
                            var isolationLevel = (IsolationLevel)TransactionRollbackBefore.IsolationLevel.Fetch(evnt.Value);
                            var dataSource = (string)TransactionRollbackBefore.DataSource.Fetch(connection);
                            var database = (string)TransactionRollbackBefore.Database.Fetch(connection);

                                var telemetry = new DependencyTelemetry()
                            {
                                Id = operationId.ToStringInvariant("N"),
                                Name = string.Join(" | ", dataSource, database, operation, isolationLevel),
                                Type = RemoteDependencyConstants.SQL,
                                Target = string.Join(" | ", dataSource, database),
                                Data = operation,
                                Success = true,
                            };

                            InitializeTelemetry(telemetry, operationId, timestamp);

                            this.operationHolder.Store(connection, Tuple.Create(telemetry, /* isCustomCreated: */ false));
                        }
                        else
                        {
                            DependencyCollectorEventSource.Log.TrackingAnExistingTelemetryItemVerbose();
                        }

                        break;
                    }

                    case SqlAfterCommitTransaction:
                    case SqlMicrosoftAfterCommitTransaction:
                    {
                        var operationId = (Guid)TransactionCommitAfter.OperationId.Fetch(evnt.Value);

                        DependencyCollectorEventSource.Log.SqlClientDiagnosticSubscriberCallbackCalled(operationId,
                            evnt.Key);

                        var connection = TransactionCommitAfter.Connection.Fetch(evnt.Value);
                        var tuple = this.operationHolder.Get(connection);

                        if (tuple != null)
                        {
                            this.operationHolder.Remove(connection);

                            var telemetry = tuple.Item1;

                            var timestamp = (long)TransactionCommitAfter.Timestamp.Fetch(evnt.Value);

                            telemetry.Stop(timestamp);

                            this.client.TrackDependency(telemetry);
                        }
                        else
                        {
                            DependencyCollectorEventSource.Log.EndCallbackWithNoBegin(
                                operationId.ToStringInvariant("N"));
                        }

                        break;
                    }

                    case SqlAfterRollbackTransaction:
                    case SqlMicrosoftAfterRollbackTransaction:
                        {
                        var operationId = (Guid)TransactionRollbackAfter.OperationId.Fetch(evnt.Value);

                        DependencyCollectorEventSource.Log.SqlClientDiagnosticSubscriberCallbackCalled(operationId, evnt.Key);

                        var connection = TransactionRollbackAfter.Connection.Fetch(evnt.Value);
                        var tuple = this.operationHolder.Get(connection);

                        if (tuple != null)
                        {
                            this.operationHolder.Remove(connection);

                            var telemetry = tuple.Item1;

                            var timestamp = (long)TransactionRollbackAfter.Timestamp.Fetch(evnt.Value);

                            telemetry.Stop(timestamp);

                            this.client.TrackDependency(telemetry);
                        }
                        else
                        {
                            DependencyCollectorEventSource.Log.EndCallbackWithNoBegin(operationId.ToStringInvariant("N"));
                        }

                        break;
                    }

                    case SqlErrorCommitTransaction:
                    case SqlMicrosoftErrorCommitTransaction:
                        {
                        var operationId = (Guid)TransactionCommitError.OperationId.Fetch(evnt.Value);

                        DependencyCollectorEventSource.Log.SqlClientDiagnosticSubscriberCallbackCalled(operationId, evnt.Key);

                        var connection = TransactionCommitError.Connection.Fetch(evnt.Value);
                        var tuple = this.operationHolder.Get(connection);

                        if (tuple != null)
                        {
                            this.operationHolder.Remove(connection);

                            var telemetry = tuple.Item1;

                            var timestamp = (long)TransactionCommitError.Timestamp.Fetch(evnt.Value);

                            telemetry.Stop(timestamp);

                            var exception = (Exception)TransactionCommitError.Exception.Fetch(evnt.Value);

                            ConfigureExceptionTelemetry(telemetry, exception);

                            this.client.TrackDependency(telemetry);
                        }
                        else
                        {
                            DependencyCollectorEventSource.Log.EndCallbackWithNoBegin(operationId.ToStringInvariant("N"));
                        }

                        break;
                    }

                    case SqlErrorRollbackTransaction:
                    case SqlMicrosoftErrorRollbackTransaction:
                        {
                        var operationId = (Guid)TransactionRollbackError.OperationId.Fetch(evnt.Value);

                        DependencyCollectorEventSource.Log.SqlClientDiagnosticSubscriberCallbackCalled(operationId, evnt.Key);

                        var connection = TransactionRollbackError.Connection.Fetch(evnt.Value);
                        var tuple = this.operationHolder.Get(connection);

                        if (tuple != null)
                        {
                            this.operationHolder.Remove(connection);

                            var telemetry = tuple.Item1;

                            var timestamp = (long)TransactionRollbackError.Timestamp.Fetch(evnt.Value);

                            telemetry.Stop(timestamp);

                            var exception = (Exception)TransactionRollbackError.Exception.Fetch(evnt.Value);

                            ConfigureExceptionTelemetry(telemetry, exception);

                            this.client.TrackDependency(telemetry);
                        }
                        else
                        {
                            DependencyCollectorEventSource.Log.EndCallbackWithNoBegin(operationId.ToStringInvariant("N"));
                        }

                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                DependencyCollectorEventSource.Log
                    .SqlClientDiagnosticSourceListenerOnNextFailed(ExceptionUtilities.GetExceptionDetailString(ex));
            }
        }

        private static void InitializeTelemetry(DependencyTelemetry telemetry, Guid operationId, long timestamp)
        {
            telemetry.Start(timestamp);

            var activity = Activity.Current;

            if (activity != null)
            {
                telemetry.Context.Operation.Id = activity.RootId;

                // SQL Client does NOT create and Activity, i.e. 
                // we initialize SQL dependency using request Activity 
                // and it is a parent of the SQL dependency
                telemetry.Context.Operation.ParentId = activity.Id;

                foreach (var item in activity.Baggage)
                {
                    if (!telemetry.Properties.ContainsKey(item.Key))
                    {
                        telemetry.Properties[item.Key] = item.Value;
                    }
                }
            }
            else
            {
                telemetry.Context.Operation.Id = operationId.ToStringInvariant("N");
            }
        }

        private static void ConfigureExceptionTelemetry(DependencyTelemetry telemetry, Exception exception)
        {
            telemetry.Success = false;
            telemetry.Properties["Exception"] = exception.ToInvariantString();

            if (exception is SqlException sqlException)
            {
                telemetry.ResultCode = sqlException.Number.ToString(CultureInfo.InvariantCulture);
            }
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (this.subscriber != null)
                {
                    this.subscriber.Dispose();
                }
            }
        }

        private sealed class SqlClientDiagnosticSourceSubscriber : IObserver<DiagnosticListener>, IDisposable
        {
            private readonly SqlClientDiagnosticSourceListener sqlDiagnosticListener;
            private readonly IDisposable listenerSubscription;

            private IDisposable eventSubscription;

            internal SqlClientDiagnosticSourceSubscriber(SqlClientDiagnosticSourceListener listener)
            {
                this.sqlDiagnosticListener = listener;

                try
                {
                    this.listenerSubscription = DiagnosticListener.AllListeners.Subscribe(this);
                }
                catch (Exception ex)
                {
                    DependencyCollectorEventSource.Log.SqlClientDiagnosticSubscriberFailedToSubscribe(ex.ToInvariantString());
                }

                SubscriptionManager.Attach(this.sqlDiagnosticListener);
            }

            public void OnNext(DiagnosticListener value)
            {
                if (value != null)
                {
                    if (value.Name == DiagnosticListenerName)
                    {
                        this.eventSubscription = value.Subscribe(this.sqlDiagnosticListener);
                    }
                }
            }

            public void OnCompleted()
            {
            }

            public void OnError(Exception error)
            {
            }

            public void Dispose()
            {
                SubscriptionManager.Detach(this.sqlDiagnosticListener);
                this.eventSubscription?.Dispose();

                this.listenerSubscription?.Dispose();
            }
        }
    }
}