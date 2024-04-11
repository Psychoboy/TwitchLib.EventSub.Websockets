﻿using System;
using System.Net.WebSockets;
using Microsoft.Extensions.Logging;
using TwitchLib.EventSub.Websockets.Client;

namespace TwitchLib.EventSub.Websockets.Extensions
{
    internal static partial class LogExtensions
    {
        [LoggerMessage(LogLevel.Debug, "{message}")]
        public static partial void LogMessage(this ILogger<EventSubWebsocketClient> logger, string message);
        
        [LoggerMessage(LogLevel.Critical, "Websocket {sessionId} disconnected at {disconnectedAt}. Reason: {disconnectReason}")]
        public static partial void LogForceDisconnected(this ILogger<EventSubWebsocketClient> logger, string sessionId, DateTime? disconnectedAt, string disconnectReason);
        
        [LoggerMessage(LogLevel.Warning, "Websocket reconnect for SessionId {sessionId} requested!")]
        public static partial void LogReconnectRequested(this ILogger<EventSubWebsocketClient> logger, string sessionId);
        
        [LoggerMessage(LogLevel.Error, "Websocket reconnect for SessionId {sessionId} failed!")]
        public static partial void LogReconnectFailed(this ILogger<EventSubWebsocketClient> logger, string sessionId);
        
        [LoggerMessage(LogLevel.Warning, "Found unknown message type: {messageType}")]
        public static partial void LogUnknownMessageType(this ILogger<EventSubWebsocketClient> logger, string messageType);
        
        [LoggerMessage(LogLevel.Critical, "{closeStatus} - {closeStatusDescription}")]
        public static partial void LogWebsocketClosed(this ILogger<WebsocketClient> logger, WebSocketCloseStatus closeStatus, string closeStatusDescription);
    }
}