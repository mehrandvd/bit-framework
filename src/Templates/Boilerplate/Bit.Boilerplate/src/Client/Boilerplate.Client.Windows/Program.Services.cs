﻿//+:cnd:noEmit
using Microsoft.Extensions.Logging;
using System.Security.Authentication;
using Boilerplate.Client.Windows.Services;

namespace Boilerplate.Client.Windows;

public static partial class Program
{
    public static void AddClientWindowsProjectServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Services being registered here can get injected in windows project only.
        services.AddClientCoreProjectServices(configuration);

        services.AddScoped<IWebAuthnService, WindowsWebAuthnService>();
        services.AddScoped<IExceptionHandler, WindowsExceptionHandler>();
        services.AddScoped<IAppUpdateService, WindowsAppUpdateService>();
        services.AddScoped<IBitDeviceCoordinator, WindowsDeviceCoordinator>();

        services.AddScoped(sp =>
        {
            var handler = sp.GetRequiredService<HttpMessageHandler>();
            var httpClient = new HttpClient(handler)
            {
                BaseAddress = new Uri(configuration.GetServerAddress(), UriKind.Absolute)
            };
            if (sp.GetRequiredService<ClientWindowsSettings>().WebAppUrl is Uri origin)
            {
                httpClient.DefaultRequestHeaders.Add("X-Origin", origin.ToString());
            }
            return httpClient;
        });
        services.AddKeyedScoped<HttpMessageHandler, SocketsHttpHandler>("PrimaryHttpMessageHandler", (sp, key) => new()
        {
            EnableMultipleHttp2Connections = true,
            EnableMultipleHttp3Connections = true,
            PooledConnectionLifetime = TimeSpan.FromMinutes(15),
            AutomaticDecompression = System.Net.DecompressionMethods.All,
            SslOptions = new()
            {
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            }
        });

        services.AddSingleton(sp => configuration);
        services.AddSingleton<IStorageService, WindowsStorageService>();
        services.AddSingleton<ILocalHttpServer, WindowsLocalHttpServer>();

        ClientWindowsSettings settings = new();
        configuration.Bind(settings);
        services.AddSingleton(sp =>
        {
            return settings;
        });
        services.AddSingleton(ITelemetryContext.Current!);
        //#if (notification == true)
        services.AddSingleton<IPushNotificationService, WindowsPushNotificationService>();
        //#endif

        services.AddWindowsFormsBlazorWebView();
        services.AddBlazorWebViewDeveloperTools();

        services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.ConfigureLoggers(configuration);
            loggingBuilder.AddEventSourceLogger();

            loggingBuilder.AddEventLog(options => configuration.GetRequiredSection("Logging:EventLog").Bind(options));
            //#if (appInsights == true)
            if (string.IsNullOrEmpty(settings.ApplicationInsights?.ConnectionString) is false)
            {
                loggingBuilder.AddApplicationInsights(config =>
                {
                    config.TelemetryInitializers.Add(new WindowsAppInsightsTelemetryInitializer());
                    configuration.GetRequiredSection("ApplicationInsights").Bind(config);
                }, options => configuration.GetRequiredSection("Logging:ApplicationInsights").Bind(options));
            }
            //#endif
        });

        services.AddOptions<ClientWindowsSettings>()
            .Bind(configuration)
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
}
