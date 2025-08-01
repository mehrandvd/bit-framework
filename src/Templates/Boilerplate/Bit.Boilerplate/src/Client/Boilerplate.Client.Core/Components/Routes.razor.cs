﻿namespace Boilerplate.Client.Core.Components;

public partial class Routes
{
    [Parameter] public Type? Layout { get; set; }

    [AutoInject] NavigationManager? navigationManager { set => universalLinksNavigationManager = value; get => universalLinksNavigationManager; }
    private static NavigationManager? universalLinksNavigationManager;

    public static async Task OpenUniversalLink(string url, bool forceLoad = false, bool replace = false)
    {
        await EnsureNavigationManagerIsReady();

        if (CultureInfoManager.InvariantGlobalization is false &&
            forceLoad == false &&
            (AppPlatform.IsAndroid || AppPlatform.IsIos))
        {
            var currentCulture = CultureInfo.CurrentUICulture.Name;
            var uri = new Uri(url);
            var urlCulture = uri.GetCulture();
            forceLoad = urlCulture is not null && string.Equals(currentCulture, urlCulture, StringComparison.InvariantCultureIgnoreCase) is false;
        }

        universalLinksNavigationManager!.NavigateTo(url, forceLoad, replace);
    }

    private static async Task EnsureNavigationManagerIsReady()
    {
        await Task.Run(async () =>
        {
            while (universalLinksNavigationManager is null)
            {
                await Task.Yield();
            }
        });
    }
}
