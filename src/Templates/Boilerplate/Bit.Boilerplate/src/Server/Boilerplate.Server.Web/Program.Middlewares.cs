﻿//+:cnd:noEmit
using System.Net;
using System.Runtime.Loader;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Localization.Routing;
using Boilerplate.Shared;
using Boilerplate.Shared.Attributes;
//#if (api == "Integrated")
using Hangfire;
using Boilerplate.Server.Api;
using Boilerplate.Server.Api.Filters;
using Boilerplate.Server.Api.Services;
//#endif
using Boilerplate.Server.Web.Endpoints;

namespace Boilerplate.Server.Web;

public static partial class Program
{
    /// <summary>
    /// https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/?view=aspnetcore-9.0#middleware-order
    /// </summary>
    public static void ConfigureMiddlewares(this WebApplication app)
    {
        var configuration = app.Configuration;
        var env = app.Environment;

        ServerWebSettings settings = new();
        configuration.Bind(settings);

        app.UseAppForwardedHeaders();

        app.UseLocalization();

        //#if (api == "Integrated")
        app.UseExceptionHandler();
        //#endif

        if (env.IsDevelopment())
        {
            app.UseWebAssemblyDebugging();
        }
        else
        {
            app.UseHttpsRedirection();
            app.UseResponseCompression();

            app.UseHsts();
            app.UseXContentTypeOptions();
            app.UseXXssProtection(options => options.EnabledWithBlockMode());
            app.UseXfo(options => options.SameOrigin());
        }

        Configure_401_403_404_Pages(app);

        if (env.IsDevelopment())
        {
            app.UseDirectoryBrowser();
        }


        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(async () =>
            {
                if (env.IsDevelopment() is false)
                {
                    // Caching static files on the Browser and CDN's edge servers.
                    if (context.Request.Query.Any(q => string.Equals(q.Key, "v", StringComparison.InvariantCultureIgnoreCase)) &&
                        env.WebRootFileProvider.GetFileInfo(context.Request.Path).Exists)
                    {
                        context.Response.GetTypedHeaders().CacheControl = new()
                        {
                            Public = true,
                            NoTransform = true,
                            MaxAge = TimeSpan.FromDays(7)
                        };
                    }
                }
            });

            await next.Invoke();
        });
        app.UseStaticFiles();

        if (string.IsNullOrEmpty(env.WebRootPath) is false && Path.Exists(Path.Combine(env.WebRootPath, @".well-known")))
        {
            // https://yurl.chayev.com/
            app.UseStaticFiles(new StaticFileOptions()
            {
                FileProvider = new PhysicalFileProvider(Path.Combine(env.WebRootPath, @".well-known")),
                RequestPath = new PathString("/.well-known"),
                DefaultContentType = "application/json",
                ServeUnknownFileTypes = true
            });
        }

        //#if (api == "Integrated")
        app.UseCors();
        //#endif

        app.UseAuthentication();
        app.UseAuthorization();

        app.UseOutputCache();

        app.UseAntiforgery();

        //#if (aspire == true)
        app.MapAspire();
        //#endif

        //#if (api == "Integrated")
        app.UseSwagger();

        app.UseSwaggerUI(options =>
        {
            options.InjectJavascript($"/_content/Boilerplate.Server.Api/scripts/swagger-utils.js?v={Environment.TickCount64}");
        });

        app.UseHangfireDashboard(options: new()
        {
            DarkModeEnabled = true,
            Authorization = [new HangfireDashboardAuthorizationFilter()]
        });

        app.MapGet("/api/minimal-api-sample/{routeParameter}", [AppResponseCache(MaxAge = 3600 * 24)] (string routeParameter, [FromQuery] string queryStringParameter) => new
        {
            RouteParameter = routeParameter,
            QueryStringParameter = queryStringParameter
        }).WithTags("Test").CacheOutput("AppResponseCachePolicy");

        //#if (signalR == true)
        if (string.IsNullOrEmpty(configuration["Azure:SignalR:ConnectionString"]) is false
            && settings.WebAppRender.BlazorMode is not BlazorWebAppMode.BlazorWebAssembly)
        {
            // Azure SignalR is going to send blazor server / auto messages to the Azure Cloud which is useless in this case,
            // because scale out lots of messages that are related to the current opened tab of browser only is not necessary and will cost you lots of money.
            // https://github.com/Azure/azure-signalr/issues/1738
            // Solutions:
            // - Switch to Blazor WebAssembly in production. Hint: To leverage Blazor server's enhanced development experience in local dev environment, you can disable Azure SignalR by setting "Azure:SignalR:ConnectionString" to null in appsettings.json or appsettings.Development.json.
            // OR
            // - Use Standalone API mode:
            //    Publish and run the Server.Api project independently to serve restful APIs and SignalR services like AppHub (Just like https://adminpanel-api.bitplatform.dev/swagger deployment)
            //    and use the Server.Web project solely as a Blazor Server or pre-rendering service provider.
            throw new InvalidOperationException("Azure SignalR is not supported with Blazor Server and Auto");
        }
        app.MapHub<Api.SignalR.AppHub>("/app-hub", options => options.AllowStatefulReconnects = true);
        //#endif

        app.MapControllers()
           .RequireAuthorization()
           .CacheOutput("AppResponseCachePolicy");
        //#endif

        app.UseSiteMap();
        app.UseWebInteropApp();

        // Handle the rest of requests with blazor
        var blazorApp = app.MapRazorComponents<Components.App>()
            .CacheOutput("AppResponseCachePolicy")
            .AddInteractiveServerRenderMode()
            .AddInteractiveWebAssemblyRenderMode()
            .AddAdditionalAssemblies(AssemblyLoadContext.Default.Assemblies.Where(asm => asm.GetName().Name?.Contains("Boilerplate.Client") is true).ToArray());

        if (settings.WebAppRender.PrerenderEnabled is false)
        {
            blazorApp.AllowAnonymous(); // Server may not check authorization for pages when there's no pre rendering, let the client handle it.
        }
    }

    /// <summary>
    /// Prior to the introduction of .NET 8, the Blazor router effectively managed NotFound and NotAuthorized components during pre-rendering.
    /// However, the current behavior has changed, and it now exclusively returns 401, 403, and 404 status codes with an empty body response!
    /// To address this, we've implemented the UseStatusCodePages middleware to handle responses featuring 401, 403, and 404 status codes that lack a body.
    /// This middleware facilitates redirection to the appropriate not-found and not-authorized pages. Consequently, the status code for these responses becomes 302 (Found).
    /// To mitigate the challenges posed by this situation, our only recourse is to repurpose the 401, 403, and 404 status codes for
    /// not-found and not-authorized responses, at the very least.
    /// </summary>
    private static void Configure_401_403_404_Pages(WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.HasValue)
            {
                if (context.Request.Path.Value.Contains(Urls.NotFoundPage, StringComparison.InvariantCultureIgnoreCase))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                }
                if (context.Request.Path.Value.Contains(Urls.NotAuthorizedPage, StringComparison.InvariantCultureIgnoreCase))
                {
                    context.Response.StatusCode = context.Request.Query["isForbidden"].FirstOrDefault() is "true" ? (int)HttpStatusCode.Forbidden : (int)HttpStatusCode.Unauthorized;
                }
            }

            await next.Invoke(context);
        });

        app.UseStatusCodePages(options: new()
        {
            HandleAsync = async (statusCodeContext) =>
            {
                var httpContext = statusCodeContext.HttpContext;

                if (httpContext.Response.StatusCode is 401 or 403 &&
                    httpContext.GetEndpoint()?.Metadata.OfType<ComponentTypeMetadata>().Any() is true /* The generation of a 401 or 403 status code is attributed to Blazor. */)
                {
                    bool is403 = httpContext.Response.StatusCode is 403;

                    var qs = AppQueryStringCollection.Parse(httpContext.Request.QueryString.Value ?? string.Empty);
                    qs.Remove("try_refreshing_token");
                    var returnUrl = UriHelper.BuildRelative(httpContext.Request.PathBase, httpContext.Request.Path, new QueryString(qs.ToString()));
                    httpContext.Response.Redirect($"{Urls.NotAuthorizedPage}?return-url={returnUrl}&isForbidden={(is403 ? "true" : "false")}");
                }
                else if (httpContext.Response.StatusCode is 404 &&
                    httpContext.GetEndpoint() is null /* Please be aware that certain endpoints, particularly those associated with web API actions, may intentionally return a 404 error. */)
                {
                    httpContext.Response.Redirect($"{Urls.NotFoundPage}?url={httpContext.Request.GetEncodedPathAndQuery()}");
                }
                else
                {
                    await statusCodeContext.Next.Invoke(statusCodeContext.HttpContext);
                }
            }
        });
    }
}
