﻿//+:cnd:noEmit
using Humanizer;
using Boilerplate.Server.Api.Services;
using Boilerplate.Shared.Dtos.Identity;
using Boilerplate.Server.Api.Models.Identity;
using Boilerplate.Shared.Controllers.Identity;
using Boilerplate.Server.Api.Services.Identity;
using Microsoft.AspNetCore.Authentication.BearerToken;
//#if (signalR == true)
using Microsoft.AspNetCore.SignalR;
using Boilerplate.Server.Api.SignalR;
//#endif

namespace Boilerplate.Server.Api.Controllers.Identity;

[ApiController, AllowAnonymous]
[Route("api/[controller]/[action]")]
public partial class IdentityController : AppControllerBase, IIdentityController
{
    [AutoInject] private IUserStore<User> userStore = default!;
    [AutoInject] private IUserEmailStore<User> userEmailStore = default!;
    [AutoInject] private UserManager<User> userManager = default!;
    [AutoInject] private SignInManager<User> signInManager = default!;
    [AutoInject] private ILogger<IdentityController> logger = default!;
    [AutoInject] private UserClaimsService userClaimsService = default!;
    [AutoInject] private IUserConfirmation<User> userConfirmation = default!;
    [AutoInject] private IUserPhoneNumberStore<User> userPhoneNumberStore = default!;
    [AutoInject] private IOptionsMonitor<BearerTokenOptions> bearerTokenOptions = default!;
    [AutoInject] private AppUserClaimsPrincipalFactory userClaimsPrincipalFactory = default!;
    //#if (signalR == true)
    [AutoInject] private IHubContext<AppHub> appHubContext = default!;
    //#endif
    //#if (notification == true)
    [AutoInject] private PushNotificationService pushNotificationService = default!;
    //#endif

    //#if (captcha == "reCaptcha")
    [AutoInject] private GoogleRecaptchaService googleRecaptchaService = default!;
    //#endif

    /// <summary>
    /// By leveraging summary tags in your controller's actions and DTO properties you can make your codes much easier to maintain.
    /// These comments will also be used in swagger docs and ui.
    /// </summary>
    [HttpPost]
    public async Task SignUp(SignUpRequestDto request, CancellationToken cancellationToken)
    {
        request.PhoneNumber = phoneService.NormalizePhoneNumber(request.PhoneNumber);
        //#if (captcha == "reCaptcha")
        if (await googleRecaptchaService.Verify(request.GoogleRecaptchaResponse, cancellationToken) is false)
            throw new BadRequestException(Localizer[nameof(AppStrings.InvalidGoogleRecaptchaResponse)]);
        //#endif

        // Attempt to locate an existing user using either their email address or phone number. The enforcement of a unique username policy is integral to the aspnetcore identity framework.
        var existingUser = await userManager.FindUserAsync(new() { Email = request.Email, PhoneNumber = request.PhoneNumber });
        if (existingUser is not null)
        {

            if (await userConfirmation.IsConfirmedAsync(userManager, existingUser) is false)
            {
                await SendConfirmationToken(existingUser, request.ReturnUrl, cancellationToken);
                throw new BadRequestException(Localizer[nameof(AppStrings.UserIsNotConfirmed)]).WithData("UserId", existingUser.Id);
            }
            else
            {
                throw new BadRequestException(Localizer[nameof(AppStrings.DuplicateEmailOrPhoneNumber)]).WithData("UserId", existingUser.Id);
            }
        }

        var userToAdd = new User { LockoutEnabled = true };

        await userStore.SetUserNameAsync(userToAdd, request.UserName!, cancellationToken);

        if (string.IsNullOrEmpty(request.Email) is false)
        {
            await userEmailStore.SetEmailAsync(userToAdd, request.Email!, cancellationToken);
        }

        if (string.IsNullOrEmpty(request.PhoneNumber) is false)
        {
            await userPhoneNumberStore.SetPhoneNumberAsync(userToAdd, request.PhoneNumber!, cancellationToken);
        }

        await userManager.CreateUserWithDemoRole(userToAdd, request.Password!);

        await SendConfirmationToken(userToAdd, request.ReturnUrl, cancellationToken);
    }

    [HttpPost, Produces<SignInResponseDto>()]
    public async Task SignIn(SignInRequestDto request, CancellationToken cancellationToken)
    {
        request.PhoneNumber = phoneService.NormalizePhoneNumber(request.PhoneNumber);

        var user = await userManager.FindUserAsync(request)
                    ?? await userManager.CreateUserWithDemoRole(request, request.Password); // Check out SignInModalService for more details

        await SignIn(request, user, cancellationToken);
    }

    private async Task SignIn(SignInRequestDto request, User user, CancellationToken cancellationToken)
    {
        signInManager.AuthenticationScheme = IdentityConstants.BearerScheme;

        var userSession = await CreateUserSession(user.Id, cancellationToken);

        if (user.TwoFactorEnabled)
        {
            // This applies only to the current short-lived access token. You can remove this line entirely.
            userClaimsPrincipalFactory.SessionClaims.Add(new(AppClaimTypes.ELEVATED_SESSION, "true"));
        }

        userClaimsPrincipalFactory.SessionClaims.Add(new(AppClaimTypes.SESSION_ID, userSession.Id.ToString()));

        bool isOtpSignIn = string.IsNullOrEmpty(request.Otp) is false;

        var (signInResult, firstStepAuthenticationMethod) = isOtpSignIn
            ? await signInManager.OtpSignInAsync(user, request.Otp!)
            : (await signInManager.PasswordSignInAsync(user!.UserName!, request.Password!, isPersistent: false, lockoutOnFailure: true), authenticationMethod: "Password");

        if (signInResult.IsNotAllowed && await userConfirmation.IsConfirmedAsync(userManager, user) is false)
        {
            try
            {
                await SendConfirmationToken(user, request.ReturnUrl, cancellationToken);
            }
            catch (TooManyRequestsExceptions) { }
            throw new BadRequestException(Localizer[nameof(AppStrings.UserIsNotConfirmed)]).WithData("UserId", user.Id);
        }

        if (signInResult.IsLockedOut)
        {
            var tryAgainIn = (user.LockoutEnd! - DateTimeOffset.UtcNow).Value;
            throw new BadRequestException(Localizer[nameof(AppStrings.UserLockedOut), tryAgainIn.Humanize(culture: CultureInfo.CurrentUICulture)]).WithData("UserId", user.Id).WithExtensionData("TryAgainIn", tryAgainIn);
        }

        if (signInResult.RequiresTwoFactor)
        {
            if (string.IsNullOrEmpty(request.TwoFactorCode) is false)
            {
                signInResult = await TwoFactorSignIn(user, request.TwoFactorCode);
            }
            else
            {
                await Response.WriteAsJsonAsync(new SignInResponseDto { RequiresTwoFactor = true }, cancellationToken);
                return;
            }
        }

        if (signInResult.Succeeded is false)
            throw new UnauthorizedException(Localizer[nameof(AppStrings.InvalidUserCredentials)]).WithData(new() { { "UserId", user.Id }, { "Identifier", request } });

        await DbContext.UserSessions.AddAsync(userSession, cancellationToken);
        user.TwoFactorTokenRequestedOn = null;
        var addUserSessionResult = await userManager.UpdateAsync(user);
        if (addUserSessionResult.Succeeded is false)
            throw new ResourceValidationException(addUserSessionResult.Errors.Select(e => new LocalizedString(e.Code, e.Description)).ToArray()).WithData("UserId", user.Id);
        await DbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Microsoft.AspNetCore.Identity.SignInResult> TwoFactorSignIn(User user, string code)
    {
        var result = await signInManager.TwoFactorRecoveryCodeSignInAsync(code);

        if (result.Succeeded is false)
        {
            result = await signInManager.TwoFactorSignInAsync(TokenOptions.DefaultPhoneProvider, code, false, false);
        }

        if (result.Succeeded is false)
        {
            result = await signInManager.TwoFactorAuthenticatorSignInAsync(code, false, false);
        }

        if (result.Succeeded is true && user.OtpRequestedOn != null)
        {
            await userManager.ResetAccessFailedCountAsync(user);
            user.OtpRequestedOn = null; // invalidates the OTP
            var updateResult = await userManager.UpdateAsync(user);
            if (updateResult.Succeeded is false)
                throw new ResourceValidationException(updateResult.Errors.Select(e => new LocalizedString(e.Code, e.Description)).ToArray()).WithData("UserId", user.Id);
        }

        return result;
    }

    /// <summary>
    /// Creates a user session and adds its ID to the access and refresh tokens, but only if the sign-in is successful <see cref="AppUserClaimsPrincipalFactory.SessionClaims"/>
    /// </summary>
    private async Task<UserSession> CreateUserSession(Guid userId, CancellationToken cancellationToken)
    {
        var userSession = new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StartedOn = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            IP = HttpContext.Connection.RemoteIpAddress?.ToString(),
            // Relying on Cloudflare cdn to retrieve address.
            // https://developers.cloudflare.com/rules/transform/managed-transforms/reference/#add-visitor-location-headers
            Address = $"{Request.Headers["cf-ipcountry"]}, {Request.Headers["cf-ipcity"]}"
        };

        await UpdateUserSessionPrivilegeStatus(userSession, cancellationToken);

        return userSession;
    }

    /// <summary>
    /// <inheritdoc cref="AuthPolicies.PRIVILEGED_ACCESS"/>
    /// </summary>
    private async Task UpdateUserSessionPrivilegeStatus(UserSession userSession, CancellationToken cancellationToken)
    {
        var userId = userSession.UserId;

        var maxPrivilegedSessionsClaimValues = await userClaimsService.GetClaimValues<int?>(userId, AppClaimTypes.MAX_PRIVILEGED_SESSIONS, cancellationToken);

        var hasUnlimitedPrivilegedSessions = maxPrivilegedSessionsClaimValues.Any(v => v == -1); // -1 means no limit

        var maxPrivilegedSessionsCount = hasUnlimitedPrivilegedSessions ? -1 : maxPrivilegedSessionsClaimValues.Max() ?? AppSettings.Identity.MaxPrivilegedSessionsCount; // If no claim is found, use the default value from app settings.

        var isPrivileged = hasUnlimitedPrivilegedSessions ||
            userSession.Privileged is true || // Once session gets privileged, it stays privileged until gets deleted.
            await DbContext.UserSessions.CountAsync(us => us.UserId == userSession.UserId && us.Privileged == true, cancellationToken) < maxPrivilegedSessionsCount;

        userClaimsPrincipalFactory.SessionClaims.Add(new(AppClaimTypes.PRIVILEGED_SESSION, isPrivileged ? "true" : "false"));
        userClaimsPrincipalFactory.SessionClaims.Add(new(AppClaimTypes.MAX_PRIVILEGED_SESSIONS, maxPrivilegedSessionsCount.ToString(CultureInfo.InvariantCulture)));

        userSession.Privileged = isPrivileged;
    }

    [HttpPost]
    public async Task<ActionResult<TokenResponseDto>> Refresh(RefreshTokenRequestDto request, CancellationToken cancellationToken)
    {
        UserSession? userSession = null;

        try
        {
            var refreshTokenProtector = bearerTokenOptions.Get(IdentityConstants.BearerScheme).RefreshTokenProtector;
            var refreshTicket = refreshTokenProtector.Unprotect(request.RefreshToken);

            if (refreshTicket?.Principal?.IsAuthenticated() is not true)
                throw new UnauthorizedException();

            var currentSessionId = refreshTicket.Principal.GetSessionId();
            userSession = await DbContext.UserSessions
                .FirstOrDefaultAsync(us => us.Id == currentSessionId, cancellationToken) ?? throw new UnauthorizedException().WithData("UserSessionId", currentSessionId); // User session has been deleted.

            if ((refreshTicket.Properties.ExpiresUtc ?? DateTimeOffset.MinValue) < DateTimeOffset.UtcNow)
                throw new UnauthorizedException(); // refresh token is expired.

            var user = await signInManager.ValidateSecurityStampAsync(refreshTicket.Principal) ?? throw new UnauthorizedException(); // Security stamp has been updated (for example after 2fa configuration)
            var userId = refreshTicket.Principal.GetUserId().ToString();

            if (string.IsNullOrEmpty(request.ElevatedAccessToken) is false)
            {
                var tokenIsValid = await userManager.VerifyUserTokenAsync(user, TokenOptions.DefaultPhoneProvider, FormattableString.Invariant($"ElevatedAccess:{userSession.Id},{user.ElevatedAccessTokenRequestedOn?.ToUniversalTime()}"), request.ElevatedAccessToken)
                    || await userManager.VerifyTwoFactorTokenAsync(user, userManager.Options.Tokens.AuthenticatorTokenProvider, request.ElevatedAccessToken);
                if (tokenIsValid is false)
                {
                    await userManager.AccessFailedAsync(user);
                    throw new BadRequestException(nameof(AppStrings.InvalidToken)).WithData("UserId", user.Id);
                }
                else
                {
                    user.ElevatedAccessTokenRequestedOn = null; // invalidates token
                    await ((IUserLockoutStore<User>)userStore).ResetAccessFailedCountAsync(user, cancellationToken);
                    userClaimsPrincipalFactory.SessionClaims.Add(new(AppClaimTypes.ELEVATED_SESSION, "true"));
                }
            }

            userSession.RenewedOn = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            // Relying on Cloudflare cdn to retrieve address.
            // https://developers.cloudflare.com/rules/transform/managed-transforms/reference/#add-visitor-location-headers
            (userSession.IP, userSession.Address) = (HttpContext.Connection.RemoteIpAddress?.ToString(), $"{Request.Headers["cf-ipcountry"]}, {Request.Headers["cf-ipcity"]}");

            userClaimsPrincipalFactory.SessionClaims.Add(new(AppClaimTypes.SESSION_ID, currentSessionId.ToString()));

            await UpdateUserSessionPrivilegeStatus(userSession, cancellationToken);

            var newPrincipal = await signInManager.CreateUserPrincipalAsync(user!);

            return SignIn(newPrincipal, authenticationScheme: IdentityConstants.BearerScheme);
        }
        catch (UnauthorizedException) when (userSession is not null)
        {
            DbContext.UserSessions.Remove(userSession);
            throw;
        }
        finally
        {
            await DbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// For either otp or magic link
    /// </summary>
    [HttpPost]
    public async Task SendOtp(IdentityRequestDto request, string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        request.PhoneNumber = phoneService.NormalizePhoneNumber(request.PhoneNumber);
        var user = await userManager.FindUserAsync(request)
                    ?? await userManager.CreateUserWithDemoRole(request); // Check out SignInModalService for more details

        if (await userConfirmation.IsConfirmedAsync(userManager, user) is false)
        {
            await SendConfirmationToken(user, request.ReturnUrl, cancellationToken);
            throw new BadRequestException(Localizer[nameof(AppStrings.UserIsNotConfirmed)]).WithData("UserId", user.Id);
        }

        var resendDelay = (DateTimeOffset.Now - user.OtpRequestedOn) - AppSettings.Identity.OtpTokenLifetime;

        if (resendDelay < TimeSpan.Zero)
            throw new TooManyRequestsExceptions(Localizer[nameof(AppStrings.WaitForOtpRequestResendDelay), resendDelay.Value.Humanize(culture: CultureInfo.CurrentUICulture)]).WithData("UserId", user.Id).WithExtensionData("TryAgainIn", resendDelay);

        var (magicLinkToken, url) = await GenerateAutomaticSignInLink(user, returnUrl, originalAuthenticationMethod: "Email");

        var link = new Uri(HttpContext.Request.GetWebAppUrl(), url);

        List<Task> sendMessagesTasks = [];

        if (await userManager.IsEmailConfirmedAsync(user))
        {
            sendMessagesTasks.Add(emailService.SendOtp(user, magicLinkToken, link, cancellationToken));
        }

        if (await userManager.IsPhoneNumberConfirmedAsync(user))
        {
            var token = await userManager.GenerateUserTokenAsync(user, TokenOptions.DefaultPhoneProvider, FormattableString.Invariant($"Otp_Sms,{user.OtpRequestedOn?.ToUniversalTime()}"));
            var message = Localizer[nameof(AppStrings.OtpShortText), token].ToString();
            var smsMessage = $"{message}{Environment.NewLine}@{HttpContext.Request.GetWebAppUrl().Host} #{token}" /* Web OTP */;
            sendMessagesTasks.Add(phoneService.SendSms(smsMessage, user.PhoneNumber!));
        }

        //#if (signalR == true || notification == true)
        var pushMessage = Localizer[nameof(AppStrings.OtpShortText), await userManager.GenerateUserTokenAsync(user, TokenOptions.DefaultPhoneProvider, FormattableString.Invariant($"Otp_Push,{user.OtpRequestedOn?.ToUniversalTime()}"))].ToString();
        //#endif

        //#if (signalR == true)
        var userConnectionIds = await DbContext.UserSessions
            .Where(us => us.NotificationStatus == UserSessionNotificationStatus.Allowed && us.UserId == user.Id)
            .Select(us => us.SignalRConnectionId!)
            .ToArrayAsync(cancellationToken);
        sendMessagesTasks.Add(appHubContext.Clients.Clients(userConnectionIds).SendAsync(SignalREvents.SHOW_MESSAGE, pushMessage, null, cancellationToken));
        //#endif

        //#if (notification == true)
        sendMessagesTasks.Add(pushNotificationService.RequestPush(message: pushMessage, userRelatedPush: true, customSubscriptionFilter: s => s.UserSession!.UserId == user.Id, cancellationToken: cancellationToken));
        //#endif

        await Task.WhenAll(sendMessagesTasks);
    }

    [HttpPost]
    public async Task SendTwoFactorToken(SignInRequestDto request, CancellationToken cancellationToken)
    {
        request.PhoneNumber = phoneService.NormalizePhoneNumber(request.PhoneNumber);

        var user = await userManager.FindUserAsync(request)
                    ?? throw new ResourceNotFoundException(Localizer[nameof(AppStrings.UserNotFound)]).WithData("Identifier", request);

        await SendTwoFactorToken(request, user, cancellationToken);
    }

    private async Task SendTwoFactorToken(SignInRequestDto request, User user, CancellationToken cancellationToken)
    {
        if (user.TwoFactorEnabled is false)
            throw new BadRequestException().WithData("UserId", user.Id);

        bool isOtpSignIn = string.IsNullOrEmpty(request.Otp) is false;

        var (signInResult, firstStepAuthenticationMethod) = isOtpSignIn
            ? await signInManager.OtpSignInAsync(user, request.Otp!)
            : (await signInManager.PasswordSignInAsync(user!.UserName!, request.Password!, isPersistent: false, lockoutOnFailure: true), authenticationMethod: "Password");

        if (signInResult.RequiresTwoFactor is false)
            throw new BadRequestException().WithData("UserId", user.Id);

        var resendDelay = (DateTimeOffset.Now - user.TwoFactorTokenRequestedOn) - AppSettings.Identity.TwoFactorTokenLifetime;

        if (resendDelay < TimeSpan.Zero)
            throw new TooManyRequestsExceptions(Localizer[nameof(AppStrings.WaitForTwoFactorTokenRequestResendDelay), resendDelay.Value.Humanize(culture: CultureInfo.CurrentUICulture)]).WithData("UserId", user.Id).WithExtensionData("TryAgainIn", resendDelay);

        user.TwoFactorTokenRequestedOn = DateTimeOffset.Now;
        var result = await userManager.UpdateAsync(user);
        if (result.Succeeded is false)
            throw new ResourceValidationException(result.Errors.Select(e => new LocalizedString(e.Code, e.Description)).ToArray()).WithData("UserId", user.Id);

        var token = await userManager.GenerateTwoFactorTokenAsync(user, TokenOptions.DefaultPhoneProvider);

        List<Task> sendMessagesTasks = [];

        if (firstStepAuthenticationMethod != "Email" && await userManager.IsEmailConfirmedAsync(user))
        {
            sendMessagesTasks.Add(emailService.SendTwoFactorToken(user, token, cancellationToken));
        }

        var message = Localizer[nameof(AppStrings.TwoFactorTokenShortText), token].ToString();

        if (firstStepAuthenticationMethod != "Sms" && await userManager.IsPhoneNumberConfirmedAsync(user))
        {
            var smsMessage = $"{message}{Environment.NewLine}@{HttpContext.Request.GetWebAppUrl().Host} #{token}" /* Web OTP */;
            sendMessagesTasks.Add(phoneService.SendSms(smsMessage, user.PhoneNumber!));
        }

        if (firstStepAuthenticationMethod != "Push")
        {
            //#if (signalR == true)
            var userConnectionIds = await DbContext.UserSessions
                .Where(us => us.NotificationStatus == UserSessionNotificationStatus.Allowed && us.UserId == user.Id)
                .Select(us => us.SignalRConnectionId!)
                .ToArrayAsync(cancellationToken);
            sendMessagesTasks.Add(appHubContext.Clients.Clients(userConnectionIds).SendAsync(SignalREvents.SHOW_MESSAGE, message, null, cancellationToken));
            //#endif
            //#if (notification == true)
            sendMessagesTasks.Add(pushNotificationService.RequestPush(message: message, userRelatedPush: true, customSubscriptionFilter: s => s.UserSession!.UserId == user.Id, cancellationToken: cancellationToken));
            //#endif
        }

        await Task.WhenAll(sendMessagesTasks);
    }

    private async Task<(string token, string url)> GenerateAutomaticSignInLink(User user, string? returnUrl, string originalAuthenticationMethod)
    {
        user.OtpRequestedOn = DateTimeOffset.Now;

        var result = await userManager.UpdateAsync(user);

        if (result.Succeeded is false)
            throw new ResourceValidationException(result.Errors.Select(e => new LocalizedString(e.Code, e.Description)).ToArray()).WithData("UserId", user.Id);

        var token = await userManager.GenerateUserTokenAsync(user, TokenOptions.DefaultPhoneProvider, FormattableString.Invariant($"Otp_{originalAuthenticationMethod},{user.OtpRequestedOn?.ToUniversalTime()}"));

        var qs = $"userName={Uri.EscapeDataString(user.UserName!)}";

        if (string.IsNullOrEmpty(returnUrl) is false)
        {
            qs += $"&return-url={Uri.EscapeDataString(returnUrl)}";
        }

        var url = $"{Urls.SignInPage}?otp={Uri.EscapeDataString(token)}&{qs}&culture={CultureInfo.CurrentUICulture.Name}";

        return (token, url);
    }

    private async Task SendConfirmationToken(User user, string? returnUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(user.Email) is false)
        {
            await SendConfirmEmailToken(user, returnUrl, cancellationToken);
        }

        if (string.IsNullOrEmpty(user.PhoneNumber) is false)
        {
            await SendConfirmPhoneToken(user, cancellationToken);
        }
    }
}
