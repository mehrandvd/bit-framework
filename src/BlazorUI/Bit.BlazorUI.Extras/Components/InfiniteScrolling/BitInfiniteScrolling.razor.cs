﻿namespace Bit.BlazorUI;

/// <summary>
/// BitInfiniteScrolling is a container that enables scrolling through a list of items infinitely as long as there are items to fetch and render.
/// </summary>
public partial class BitInfiniteScrolling<TItem> : BitComponentBase
{
    private List<TItem> _currentItems = [];
    private CancellationTokenSource? _globalCts;
    private ElementReference _lastElementRef = default!;
    private BitInfiniteScrollingItemsProvider<TItem>? _itemsProvider;
    private DotNetObjectReference<BitInfiniteScrolling<TItem>>? _dotnetObj;



    private bool _isLoading => _globalCts is not null;



    [Inject] private IJSRuntime _js { get; set; } = default!;



    /// <summary>
    /// The custom template to render each item.
    /// </summary>
    [Parameter] public RenderFragment<TItem>? ChildContent { get; set; }

    /// <summary>
    /// The custom template to render when there is no item available.
    /// </summary>
    [Parameter] public RenderFragment? EmptyTemplate { get; set; }

    /// <summary>
    /// The item provider function that will be called when scrolling ends.
    /// </summary>
    [Parameter] public BitInfiniteScrollingItemsProvider<TItem>? ItemsProvider { get; set; }

    /// <summary>
    /// Alias for ChildContent.
    /// </summary>
    [Parameter] public RenderFragment<TItem>? ItemTemplate { get; set; }

    /// <summary>
    /// The CSS class of the last element that triggers the loading.
    /// </summary>
    [Parameter] public string? LastElementClass { get; set; }

    /// <summary>
    /// The height of the last element that triggers the loading.
    /// </summary>
    [Parameter] public string? LastElementHeight { get; set; }

    /// <summary>
    /// The CSS style of the last element that triggers the loading.
    /// </summary>
    [Parameter] public string? LastElementStyle { get; set; }

    /// <summary>
    /// The custom template to render while loading the new items.
    /// </summary>
    [Parameter] public RenderFragment? LoadingTemplate { get; set; }

    /// <summary>
    /// Pre-loads the data at the initialization of the component. Useful in prerendering mode.
    /// </summary>
    [Parameter] public bool Preload { get; set; }

    /// <summary>
    /// The CSS selector of the scroll container, by default the root element of the component is selected for this purpose.
    /// </summary>
    [Parameter] public string? ScrollerSelector { get; set; }

    /// <summary>
    /// The threshold parameter for the IntersectionObserver that specifies a ratio of intersection area to total bounding box area of the last element.
    /// </summary>
    [Parameter] public decimal? Threshold { get; set; }




    /// <summary>
    /// Refreshes the items and re-renders them from scratch.
    /// </summary>
    public async Task RefreshDataAsync()
    {
        _globalCts?.Cancel();
        _globalCts = null;

        _currentItems = [];
        await LoadMoreItems();
    }



    [JSInvokable("Load")]
    public async Task _LoadMoreItems()
    {
        await LoadMoreItems();
    }



    protected override async Task OnInitializedAsync()
    {
        if (Preload)
        {
            _currentItems = await LoadMoreItems();
        }

        await base.OnInitializedAsync();
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        if (ItemsProvider != _itemsProvider && (_itemsProvider is not null || _currentItems.Count == 0))
        {
            _currentItems = [];
        }

        _itemsProvider = ItemsProvider;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotnetObj = DotNetObjectReference.Create(this);
            await _js.BitInfiniteScrollingSetup(UniqueId, ScrollerSelector, RootElement, _lastElementRef, Threshold, _dotnetObj);
        }

        await base.OnAfterRenderAsync(firstRender);
    }



    protected override string RootElementClass => "bit-isc";



    private async Task<List<TItem>> LoadMoreItems()
    {
        if (ItemsProvider is null || _globalCts is not null) return [];

        var items = _currentItems;
        var localCts = new CancellationTokenSource();

        _globalCts = localCts;

        try
        {
            StateHasChanged();

            try
            {
                var newItems = await ItemsProvider(new(items.Count, localCts.Token));

                if (localCts.IsCancellationRequested is false)
                {
                    var length = items.Count;
                    items.AddRange(newItems);

                    if (items.Count != length)
                    {
                        await _js.BitInfiniteScrollingReobserve(UniqueId, _lastElementRef);
                    }
                }
            }
            catch (OperationCanceledException oce) when (oce.CancellationToken == localCts.Token) { }
        }
        finally
        {
            _globalCts = null;
            localCts.Dispose();
        }

        StateHasChanged();
        return items;
    }

    private string GetLastElementStyle()
    {
        if (_isLoading) return "display:none";


        var style = $"height:{(LastElementHeight is null ? "1px" : LastElementHeight)};width:100%";

        if (LastElementStyle is not null)
        {
            style += $";{LastElementStyle}";
        }

        return style;
    }



    protected override async ValueTask DisposeAsync(bool disposing)
    {
        if (IsDisposed || disposing is false) return;

        if (_globalCts is not null)
        {
            _globalCts.Dispose();
            _globalCts = null;
        }

        _dotnetObj?.Dispose();

        try
        {
            await _js.BitInfiniteScrollingDispose(UniqueId);
        }
        catch (JSDisconnectedException) { } // we can ignore this exception here

        await base.DisposeAsync(disposing);
    }
}
