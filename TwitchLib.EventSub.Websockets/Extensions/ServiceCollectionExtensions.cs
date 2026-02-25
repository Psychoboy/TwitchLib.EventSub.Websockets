using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System;
using TwitchLib.EventSub.Websockets.Client;

namespace TwitchLib.EventSub.Websockets.Extensions
{
    /// <summary>
    /// Static class containing extension methods for the IServiceCollection of the DI Container
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Add TwitchLib EventSub Websockets and its needed parts to the DI container
        /// </summary>
        /// <param name="services">ServiceCollection of the DI Container</param>
        /// <returns>the IServiceCollection to enable further fluent additions to it</returns>
        public static IServiceCollection AddTwitchLibEventSubWebsockets(this IServiceCollection services)
        {
            services.TryAddTransient<WebsocketClient>();
            services.TryAddSingleton(x => new EventSubWebsocketClient(x.GetRequiredService<ILogger<EventSubWebsocketClient>>(), x.GetRequiredService<IServiceProvider>(), x.GetRequiredService<WebsocketClient>()));
            return services;
        }
    }
}
