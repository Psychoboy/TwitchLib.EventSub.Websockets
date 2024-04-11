﻿using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TwitchLib.EventSub.Core;
using TwitchLib.EventSub.Websockets.Core.EventArgs;

#if NET6_0_OR_GREATER
using System.Buffers;
#endif

namespace TwitchLib.EventSub.Websockets.Client
{
    /// <summary>
    /// Websocket client to connect to variable websocket servers
    /// </summary>
    public class WebsocketClient : IDisposable
    {
        /// <summary>
        /// Determines if the Client is still connected based on WebsocketState
        /// </summary>
        public bool IsConnected => _webSocket.State == WebSocketState.Open;
        /// <summary>
        /// Determines if the Client is has encountered an unrecoverable issue based on WebsocketState
        /// </summary>
        public bool IsFaulted => _webSocket.CloseStatus != WebSocketCloseStatus.Empty && _webSocket.CloseStatus != WebSocketCloseStatus.NormalClosure;

        internal event AsyncEventHandler<DataReceivedArgs> OnDataReceived;
        internal event AsyncEventHandler<ErrorOccuredArgs> OnErrorOccurred;

        private readonly ClientWebSocket _webSocket;
        private readonly ILogger<WebsocketClient> _logger;

        /// <summary>
        /// Constructor to create a new Websocket client with a logger
        /// </summary>
        /// <param name="logger">Logger used by the websocket client to print various state info</param>
        public WebsocketClient(ILogger<WebsocketClient> logger = null)
        {
            _webSocket = new ClientWebSocket();
            _logger = logger;
        }

        /// <summary>
        /// Connects the websocket client to a given Websocket Server
        /// </summary>
        /// <param name="url">Websocket Server URL to connect to</param>
        /// <returns>true: if the connection is already open or was successful false: if the connection failed to be established</returns>
        public async Task<bool> ConnectAsync(Uri url)
        {
            try
            {
                if (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.Connecting)
                    return true;

                await _webSocket.ConnectAsync(url, CancellationToken.None);

#pragma warning disable 4014
                Task.Run(async () => await ProcessDataAsync());
#pragma warning restore 4014

                return IsConnected;
            }
            catch (Exception ex)
            {
                OnErrorOccurred?.Invoke(this, new ErrorOccuredArgs { Exception = ex });
                return false;
            }
        }

        /// <summary>
        /// Disconnect the Websocket client from its currently connected server
        /// </summary>
        /// <returns>true: if the disconnect was successful without errors false: if the client encountered an issue during the disconnect</returns>
        public async Task<bool> DisconnectAsync()
        {
            try
            {
                if (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.Connecting)
                    await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, string.Empty, CancellationToken.None);

                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred?.Invoke(this, new ErrorOccuredArgs { Exception = ex });
                return false;
            }
        }

#if NET6_0_OR_GREATER
        /// <summary>
        /// Background operation to process incoming data via the websocket
        /// </summary>
        /// <returns>Task representing the background operation</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        private async Task ProcessDataAsync()
        {
            const int minimumBufferSize = 256;
            var storeSize = 4096;
            var decoder = Encoding.UTF8.GetDecoder();

            var store = MemoryPool<byte>.Shared.Rent(storeSize).Memory;
            var buffer = MemoryPool<byte>.Shared.Rent(minimumBufferSize).Memory;

            var payloadSize = 0;
            while (IsConnected)
            {
                try
                {
                    ValueWebSocketReceiveResult receiveResult;
                    do
                    {
                        receiveResult = await _webSocket.ReceiveAsync(buffer, CancellationToken.None);
                        
                        if (payloadSize + receiveResult.Count >= storeSize)
                        {
                            storeSize *= 2;
                            var newStore = MemoryPool<byte>.Shared.Rent(storeSize).Memory;
                            store.CopyTo(newStore);
                            store = newStore;
                        }

                        if (payloadSize + receiveResult.Count >= storeSize)
                        {
                            storeSize *= 2;
                            var newStore = MemoryPool<byte>.Shared.Rent(storeSize).Memory;
                            store.CopyTo(newStore);
                            store = newStore;
                        }

                        buffer.CopyTo(store[payloadSize..]);

                        payloadSize += receiveResult.Count;
                    } while (!receiveResult.EndOfMessage);
                    switch (receiveResult.MessageType)
                    {
                        case WebSocketMessageType.Text:
                            {
                                var intermediate = MemoryPool<char>.Shared.Rent(payloadSize).Memory;
                                if (payloadSize == 0)
                                    continue;
                                decoder.Convert(store.Span[..payloadSize], intermediate.Span, true, out _, out var charsCount, out _);
                                var message = intermediate[..charsCount];
                                OnDataReceived?.Invoke(this, new DataReceivedArgs { Message = message.Span.ToString() });
                                payloadSize = 0;
                                break;
                            }
                        case WebSocketMessageType.Binary:
                            break;
                        case WebSocketMessageType.Close:
                            _logger?.LogCritical($"{(WebSocketCloseStatus)_webSocket.CloseStatus!} - {_webSocket.CloseStatusDescription!}");
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                catch (Exception ex)
                {
                    OnErrorOccurred?.Invoke(this, new ErrorOccuredArgs { Exception = ex });
                    break;
                }
            }
        }
#else
        /// <summary>
        /// Background operation to process incoming data via the websocket
        /// </summary>
        /// <returns>Task representing the background operation</returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        private async Task ProcessDataAsync()
        {
            const int minimumBufferSize = 8192;
            var buffer = new ArraySegment<byte>(new byte[minimumBufferSize]);
            var payloadSize = 0;
            
            while (IsConnected)
            {
                try
                {
                    WebSocketReceiveResult receiveResult;
                    var memory = new MemoryStream();
                    do
                    {
                        receiveResult = await _webSocket.ReceiveAsync(buffer, CancellationToken.None);
                        if (buffer.Array == null)
                            continue;
                        memory.Write(buffer.Array, buffer.Offset, receiveResult.Count);
                        payloadSize += receiveResult.Count;
                    } while (!receiveResult.EndOfMessage);
                    switch (receiveResult.MessageType)
                    {
                        case WebSocketMessageType.Text:
                        {
                            if (payloadSize == 0)
                                continue;
                            memory.Seek(0, SeekOrigin.Begin);
                            var reader = new StreamReader(memory, Encoding.UTF8);
                            OnDataReceived?.Invoke(this, new DataReceivedArgs { Message = await reader.ReadToEndAsync() });
                            memory.Dispose();
                            reader.Dispose();
                            break;
                        }
                        case WebSocketMessageType.Binary:
                            break;
                        case WebSocketMessageType.Close:
                            if (_webSocket.CloseStatus != null)
                                _logger?.LogCritical($"{(WebSocketCloseStatus) _webSocket.CloseStatus} - {_webSocket.CloseStatusDescription}");
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                catch (Exception ex)
                {
                    OnErrorOccurred?.Invoke(this, new ErrorOccuredArgs { Exception = ex });
                    break;
                }
            }
        }
#endif

        /// <summary>
        /// Cleanup of any unused resources as per IDisposable guidelines
        /// </summary>
        public void Dispose()
        {
            GC.SuppressFinalize(this);
            _webSocket.Dispose();
        }
    }
}