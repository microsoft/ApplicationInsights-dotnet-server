#if NET45

namespace Microsoft.ApplicationInsights.Extensibility.PerfCounterCollector.Implementation.QuickPulse
{
    using Helpers;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    using Microsoft.ApplicationInsights.Extensibility.Implementation.Tracing;

    internal class QuickPulseWebSocket : IQuickPulseWebSocket
    {
        private const string KeepAliveMessagePrefix = "Keep-Alive:";

        private readonly SemaphoreSlim sendGate = new SemaphoreSlim(1, 1);

        private readonly Clock timeProvider;

        private bool disposed = false;

        private readonly ClientWebSocket socket;

        private readonly Uri endpoint;

        private readonly TimeSpan keepAliveMaxTurnaroundPeriod = TimeSpan.FromSeconds(5);

        private readonly TimeSpan sessionTimeoutPeriod = TimeSpan.FromSeconds(10);

        private readonly TimeSpan keepAlivePeriod = TimeSpan.FromSeconds(3);

        private long lastSuccessfulCommunicationInTicks;

        public bool IsConnected
        {
            get
            {
                var lastSuccessfulCommunication = new DateTimeOffset(this.lastSuccessfulCommunicationInTicks, TimeSpan.Zero);
                return this.socket.State == WebSocketState.Open && lastSuccessfulCommunication - this.timeProvider.UtcNow < this.sessionTimeoutPeriod;
            }
        }

        public QuickPulseWebSocket(Uri endpoint, Clock timeProvider, IDictionary<string, string> headers)
        {
            this.socket = new ClientWebSocket();
            this.endpoint = endpoint;
            this.timeProvider = timeProvider;

            if (headers != null)
            {
                foreach (var header in headers)
                {
                    this.socket.Options.SetRequestHeader(header.Key, header.Value);
                }
            }
        }

        public Task StartAsync(Action<string> onMessage)
        {
            this.lastSuccessfulCommunicationInTicks = this.timeProvider.UtcNow.Ticks;
            
            Task.Run(
                async () => await this.socket.ConnectAsync(this.endpoint, CancellationToken.None).ContinueWith(
                    async task =>
                        {
                            // if the socket was open successfully, start the keep-alive loop
                            if (task.Status == TaskStatus.RanToCompletion && this.socket.State == WebSocketState.Open)
                            {
                                while (this.socket.State == WebSocketState.Open)
                                {
                                    try
                                    {
                                        await this.SendKeepAliveMessage().ConfigureAwait(false);
                                    }
                                    catch (Exception e)
                                    {
                                        // sending keep-alive failed, keep going
                                        QuickPulseEventSource.Log.TroubleshootingMessageEvent(
                                            string.Format(CultureInfo.InvariantCulture, "Keep-alive could not be sent. {0}", e.ToInvariantString()));
                                    }

                                    await Task.Delay(this.keepAlivePeriod).ConfigureAwait(false);
                                }
                            }
                            else
                            {
                                // socket failed to open
                                task.Exception?.Flatten();
                                QuickPulseEventSource.Log.ServiceWebSocketConnectionFailedEvent(task.Exception?.ToInvariantString());
                            }
                        }));

            return this.RunReceiveLoop(onMessage);
        }

        public async Task StopAsync()
        {
            await this.StopAsync(WebSocketCloseStatus.NormalClosure, string.Empty).ConfigureAwait(false);
        }

        // Sends a close control frame to the remote entity
        // In case of active close, waits for a close control
        // frame from the remote entity.
        private async Task StopAsync(WebSocketCloseStatus closeStatus, string closeReason)
        {
            // Wait for all pending send operations to complete.
            await this.sendGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);

            try
            {
                await this.socket.CloseAsync(closeStatus, closeReason, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                this.sendGate.Release();
            }
        }

        private async Task RunReceiveLoop(Action<string> onMessage)
        {
            // 1 Mb buffer to be safe
            byte[] receiveBuffer = new byte[1048576];
            List<byte> messageBuffer = new List<byte>();

            while (this.socket.State == WebSocketState.Open || this.socket.State == WebSocketState.CloseSent)
            {
                try
                {
                    // This call will return a close message
                    // when the websocket is closed by either entity.
                    // This in turn terminates the receive loop on Close.
                    WebSocketReceiveResult receiveResult =
                        await socket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None).ConfigureAwait(false);

                    switch (receiveResult.MessageType)
                    {
                        case WebSocketMessageType.Text:
                            messageBuffer.AddRange(new ArraySegment<byte>(receiveBuffer, 0, receiveResult.Count));

                            if (receiveResult.EndOfMessage)
                            {
                                string message = Encoding.UTF8.GetString(messageBuffer.ToArray());
                                messageBuffer.Clear();

                                if (message.StartsWith(KeepAliveMessagePrefix, StringComparison.Ordinal))
                                {
                                    // this must be a keep-alive
                                    long ticksInMessage;
                                    if (long.TryParse(message.Substring(KeepAliveMessagePrefix.Length), out ticksInMessage))
                                    {
                                        try
                                        {
                                            var timeFromMessage = new DateTimeOffset(ticksInMessage, TimeSpan.Zero);

                                            if (this.timeProvider.UtcNow - timeFromMessage < this.keepAliveMaxTurnaroundPeriod)
                                            {
                                                // the keep-alive is up to date
                                                this.lastSuccessfulCommunicationInTicks = timeFromMessage.Ticks;
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            // garbage data
                                            QuickPulseEventSource.Log.TroubleshootingMessageEvent(
                                                string.Format(
                                                    CultureInfo.InvariantCulture,
                                                    "Invalid keep-alive received from the socket. {0}",
                                                    e.ToInvariantString()));
                                        }
                                    }
                                    else
                                    {
                                        // garbage data
                                        QuickPulseEventSource.Log.TroubleshootingMessageEvent(
                                            string.Format(CultureInfo.InvariantCulture, "Invalid keep-alive received from the socket. {0}", message));
                                    }
                                }
                                else
                                {
                                    // configuration message
                                    onMessage(message);
                                }
                            }
                            break;

                        // A close control from is received 
                        // from the remote entity when the
                        // websocket is close by either entity.
                        case WebSocketMessageType.Close:
                            if (this.socket.State == WebSocketState.CloseReceived)
                            {
                                // Passive Close
                                await this.StopAsync(WebSocketCloseStatus.NormalClosure, string.Empty).ConfigureAwait(false);
                            }
                            break;

                        default:
                            await
                                this.StopAsync(
                                    WebSocketCloseStatus.InvalidMessageType,
                                    string.Format(CultureInfo.InvariantCulture, "Invalid message type received: {0}", receiveResult.MessageType))
                                    .ConfigureAwait(false);
                            break;
                    }
                }
                catch (WebSocketException ex)
                {
                    if (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
                    {
                        // Ideally, the websocket should be aborted when this 
                        // exception is caught. However, the websocket state is not
                        // updated to 'Aborted' despite the underlying TCP socket 
                        // being reset. Hence, explicitly aborting the websocket.
                        this.socket.Abort();
                    }
                }
            }
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private async Task SendKeepAliveMessage()
        {
            string message = string.Format(CultureInfo.InvariantCulture, "{0}{1}", KeepAliveMessagePrefix, timeProvider.UtcNow.Ticks);
            await this.SendMessage(message).ConfigureAwait(false);
        }

        private async Task SendMessage(string message)
        {
            await this.sendGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
            try
            {
                ArraySegment<byte> buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(message));
                await this.socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                this.sendGate.Release();
            }
        }

        private void Dispose(bool disposing)
        {
            if (!disposing || this.disposed)
            {
                return;
            }

            this.socket.Dispose();
            this.sendGate.Dispose();

            this.disposed = true;
        }
    }
}

#endif