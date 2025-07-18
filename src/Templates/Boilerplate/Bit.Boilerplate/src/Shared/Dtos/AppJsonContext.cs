﻿//+:cnd:noEmit
//#if (sample == true)
using Boilerplate.Shared.Dtos.Todo;
//#endif
//#if (module == "Admin")
using Boilerplate.Shared.Dtos.Dashboard;
//#endif
//#if (module == "Admin" || module == "Sales")
using Boilerplate.Shared.Dtos.Products;
using Boilerplate.Shared.Dtos.Categories;
//#endif
//#if (notification == true)
using Boilerplate.Shared.Dtos.PushNotification;
//#endif
//#if (signalR == true)
using Boilerplate.Shared.Dtos.Chatbot;
//#endif
using Boilerplate.Shared.Dtos.Identity;
using Boilerplate.Shared.Dtos.Statistics;
using Boilerplate.Shared.Dtos.Diagnostic;

namespace Boilerplate.Shared.Dtos;

/// <summary>
/// https://devblogs.microsoft.com/dotnet/try-the-new-system-text-json-source-generator/
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Dictionary<string, JsonElement>))]
[JsonSerializable(typeof(Dictionary<string, string?>))]
[JsonSerializable(typeof(TimeSpan))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(Guid[]))]
[JsonSerializable(typeof(GitHubStats))]
[JsonSerializable(typeof(NugetStatsDto))]
[JsonSerializable(typeof(AppProblemDetails))]
//#if (notification == true || signalR == true)
[JsonSerializable(typeof(SendNotificationToRoleDto))]
//#endif
//#if (notification == true)
[JsonSerializable(typeof(PushNotificationSubscriptionDto))]
//#endif
//#if (sample == true)
[JsonSerializable(typeof(TodoItemDto))]
[JsonSerializable(typeof(PagedResult<TodoItemDto>))]
[JsonSerializable(typeof(List<TodoItemDto>))]
//#endif
//#if (module == "Admin" || module == "Sales")
[JsonSerializable(typeof(CategoryDto))]
[JsonSerializable(typeof(List<CategoryDto>))]
[JsonSerializable(typeof(PagedResult<CategoryDto>))]
[JsonSerializable(typeof(ProductDto))]
[JsonSerializable(typeof(List<ProductDto>))]
[JsonSerializable(typeof(PagedResult<ProductDto>))]
//#endif
//#if (module == "Admin")
[JsonSerializable(typeof(List<ProductsCountPerCategoryResponseDto>))]
[JsonSerializable(typeof(OverallAnalyticsStatsDataResponseDto))]
[JsonSerializable(typeof(List<ProductPercentagePerCategoryResponseDto>))]
//#endif
[JsonSerializable(typeof(VerifyWebAuthnAndSignInRequestDto))]
[JsonSerializable(typeof(WebAuthnAssertionOptionsRequestDto))]

//#if (signalR == true)
[JsonSerializable(typeof(DiagnosticLogDto[]))]
[JsonSerializable(typeof(StartChatbotRequest))]
[JsonSerializable(typeof(SystemPromptDto))]
//#endif
public partial class AppJsonContext : JsonSerializerContext
{
}
