﻿namespace Bit.BlazorUI;

/// <summary>
/// DropMenu component is a versatile dropdown menu used in Blazor applications. It allows you to create a button that, when clicked, opens a callout or dropdown menu.
/// </summary>
public partial class BitDropMenu : BitComponentBase
{
    private string _calloutId = default!;
    private DotNetObjectReference<BitDropMenu> _dotnetObj = default!;



    [Inject] private IJSRuntime _js { get; set; } = default!;



    /// <summary>
    /// Alias of the ChildContent.
    /// </summary>
    [Parameter] public RenderFragment? Body { get; set; }

    /// <summary>
    /// The icon name of the chevron down part of the drop menu.
    /// </summary>
    [Parameter] public string? ChevronDownIcon { get; set; }

    /// <summary>
    /// The content of the callout of the drop menu.
    /// </summary>
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Custom CSS classes for different parts of the drop menu.
    /// </summary>
    [Parameter] public BitDropMenuClassStyles? Classes { get; set; }

    /// <summary>
    /// The icon to show inside the header of the drop menu.
    /// </summary>
    [Parameter] public string? IconName { get; set; }

    /// <summary>
    /// Determines the opening state of the callout of the drop menu.
    /// </summary>
    [Parameter, CallOnSet(nameof(OnSetIsOpen))]
    [ResetClassBuilder, ResetStyleBuilder, TwoWayBound]
    public bool IsOpen { get; set; }

    /// <summary>
    /// The callback is called when the drop menu is clicked.
    /// </summary>
    [Parameter] public EventCallback OnClick { get; set; }

    /// <summary>
    /// The callback is called when the drop menu is dismissed.
    /// </summary>
    [Parameter] public EventCallback OnDismiss { get; set; }

    /// <summary>
    /// The position of the responsive panel to show on the screen.
    /// </summary>
    [Parameter] public BitPanelPosition? PanelPosition { get; set; }

    /// <summary>
    /// The id of the element which needs to be scrollable in the content of the callout of the drop menu.
    /// </summary>
    [Parameter] public string? ScrollContainerId { get; set; }

    /// <summary>
    /// Renders the drop menu in responsive mode on small screens.
    /// </summary>
    [Parameter] public bool Responsive { get; set; }

    /// <summary>
    /// Custom CSS styles for different parts of the drop menu.
    /// </summary>
    [Parameter] public BitDropMenuClassStyles? Styles { get; set; }

    /// <summary>
    /// The custom content to render inside the header of the drop menu.
    /// </summary>
    [Parameter] public RenderFragment? Template { get; set; }

    /// <summary>
    /// The text to show inside the header of the drop menu.
    /// </summary>
    [Parameter] public string? Text { get; set; }

    /// <summary>
    /// Makes the background of the header of the drop menu transparent.
    /// </summary>
    [Parameter, ResetClassBuilder]
    public bool Transparent { get; set; }


    [JSInvokable("CloseCallout")]
    public async Task _CloseCalloutBeforeAnotherCalloutIsOpened()
    {
        await DismissCallout();

        StateHasChanged();
    }

    [JSInvokable("OnStart")]
    public async Task _OnStart(decimal startX, decimal startY)
    {

    }

    [JSInvokable("OnMove")]
    public async Task _OnMove(decimal diffX, decimal diffY)
    {

    }

    [JSInvokable("OnEnd")]
    public async Task _OnEnd(decimal diffX, decimal diffY)
    {

    }

    [JSInvokable("OnClose")]
    public async Task _OnClose()
    {
        await CloseCallout();
        await InvokeAsync(StateHasChanged);
    }



    protected override string RootElementClass => "bit-drm";

    protected override void RegisterCssClasses()
    {
        ClassBuilder.Register(() => Classes?.Root);

        ClassBuilder.Register(() => IsOpen ? Classes?.Opened : string.Empty);

        ClassBuilder.Register(() => Transparent ? "bit-drm-trn" : string.Empty);
    }

    protected override void RegisterCssStyles()
    {
        StyleBuilder.Register(() => Styles?.Root);

        StyleBuilder.Register(() => IsOpen ? Styles?.Opened : string.Empty);
    }

    protected override void OnInitialized()
    {
        _calloutId = $"BitDropMenu-{UniqueId}-callout";

        base.OnInitialized();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender is false) return;

        _dotnetObj = DotNetObjectReference.Create(this);

        if (Responsive is false) return;

        await _js.BitSwipesSetup(
            id: _calloutId,
            trigger: 0.25m,
            position: PanelPosition ?? BitPanelPosition.End,
            isRtl: Dir is BitDir.Rtl,
            orientationLock: BitSwipeOrientation.Horizontal,
            dotnetObj: _dotnetObj,
            isResponsive: true,
            scrollContainerId: ScrollContainerId ?? "");
    }



    private async Task HandleOnClick()
    {
        if (IsEnabled is false) return;

        await OpenCallout();

        await OnClick.InvokeAsync();
    }

    private async Task OpenCallout()
    {
        if (await AssignIsOpen(true) is false) return;

        await ToggleCallout();
    }

    private async Task CloseCallout()
    {
        await DismissCallout();

        await ToggleCallout();
    }

    private async Task ToggleCallout()
    {
        if (IsEnabled is false || IsDisposed) return;

        await _js.BitCalloutToggleCallout(
            dotnetObj: _dotnetObj,
            componentId: _Id,
            component: null,
            calloutId: _calloutId,
            callout: null,
            isCalloutOpen: IsOpen,
            responsiveMode: Responsive ? BitResponsiveMode.Panel : BitResponsiveMode.None,
            dropDirection: BitDropDirection.TopAndBottom,
            isRtl: Dir is BitDir.Rtl,
            scrollContainerId: ScrollContainerId ?? "",
            scrollOffset: 0,
            headerId: "",
            footerId: "",
            setCalloutWidth: false,
            maxWindowWidth: 0);
    }

    private void OnSetIsOpen()
    {
        _ = ToggleCallout();
    }

    private async Task DismissCallout()
    {
        if (await AssignIsOpen(false) is false) return;

        await OnDismiss.InvokeAsync();
    }

    private string GetCalloutCssClasses()
    {
        List<string> classes = ["bit-drm-cal"];

        if (Classes?.Callout is not null)
        {
            classes.Add(Classes.Callout);
        }

        if (Responsive)
        {
            classes.Add("bit-drm-res");
        }

        classes.Add(PanelPosition switch
        {
            BitPanelPosition.Start => "bit-drm-sta",
            BitPanelPosition.End => "bit-drm-end",
            BitPanelPosition.Top => "bit-drm-top",
            BitPanelPosition.Bottom => "bit-drm-btm",
            _ => "bit-drm-end"
        });

        return string.Join(' ', classes).Trim();
    }



    protected override async ValueTask DisposeAsync(bool disposing)
    {
        if (IsDisposed || disposing is false) return;

        await base.DisposeAsync(disposing);

        // _dotnetObj.Dispose(); this is handled in the js

        try
        {
            await _js.BitCalloutClearCallout(_calloutId);
            await _js.BitSwipesDispose(_calloutId);
        }
        catch (JSDisconnectedException) { } // we can ignore this exception here
    }
}
