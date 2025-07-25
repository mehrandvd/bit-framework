﻿//+:cnd:noEmit
namespace Boilerplate.Shared.Dtos.Products;

[DtoResourceType(typeof(AppStrings))]
public partial class ProductDto
{
    public Guid Id { get; set; }

    /// <summary>
    /// The product's ShortId is used to create a more human-friendly URL.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int ShortId { get; set; }

    [Required(ErrorMessage = nameof(AppStrings.RequiredAttribute_ValidationError))]
    [MaxLength(64, ErrorMessage = nameof(AppStrings.MaxLengthAttribute_InvalidMaxLength))]
    [Display(Name = nameof(AppStrings.Name))]
    public string? Name { get; set; }

    [Required(ErrorMessage = nameof(AppStrings.RequiredAttribute_ValidationError))]
    [Range(0, double.MaxValue, ErrorMessage = nameof(AppStrings.RangeAttribute_ValidationError))]
    [Display(Name = nameof(AppStrings.Price))]
    public decimal Price { get; set; }

    [MaxLength(4096, ErrorMessage = nameof(AppStrings.MaxLengthAttribute_InvalidMaxLength))]
    [Display(Name = nameof(AppStrings.Description))]
    public string? DescriptionHTML { get; set; }

    [MaxLength(4096, ErrorMessage = nameof(AppStrings.MaxLengthAttribute_InvalidMaxLength))]
    public string? DescriptionText { get; set; }

    [Required(ErrorMessage = nameof(AppStrings.RequiredAttribute_ValidationError))]
    [Display(Name = nameof(AppStrings.Category))]
    public Guid? CategoryId { get; set; }

    [Display(Name = nameof(AppStrings.Category))]
    public string? CategoryName { get; set; }

    public byte[] ConcurrencyStamp { get; set; } = [];

    public bool HasPrimaryImage { get; set; } = false;

    [Display(Name = nameof(AppStrings.AltText))]
    public string? PrimaryImageAltText { get; set; }

    public string? GetPrimaryMediumImageUrl(Uri absoluteServerAddress)
    {
        return HasPrimaryImage is false
            ? null
            : new Uri(absoluteServerAddress, $"/api/Attachment/GetAttachment/{Id}/{AttachmentKind.ProductPrimaryImageMedium}?v={ConcurrencyStamp.ToStampString()}").ToString();
    }

    public string FormattedPrice => FormatPrice();

    private string FormatPrice()
    {
        if (CultureInfoManager.InvariantGlobalization is false)
        {
            return CultureInfo.CurrentCulture.TextInfo.IsRightToLeft
                    ? $"{Price:N0} {CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol}"
                    : Price.ToString("C");
        }

        return $"${Price:N2}";
    }

    //#if (module == "Sales")
    public string PageUrl => $"{Urls.ProductPage}/{ShortId}";
    //#endif
}
