/*
 * FreeUserAuthenticator — the "no-account" heart of this project.
 *
 * The real Proton client authenticates against the Proton backend and requires
 * an account/subscription. This stub replaces it so the app is ALWAYS "logged
 * in" as a free, anonymous user and never contacts the backend.
 *
 * Result: MainWindowViewNavigator goes straight to the main (free) window —
 * the original modern Proton UI, untouched — with no login screen and no
 * account/plan/server calls.
 *
 * Swap it in with:
 *   AuthLogicModule.Load():
 *   builder.RegisterType<FreeUserAuthenticator>().As<IUserAuthenticator>().SingleInstance();
 *
 * Copyright (c) 2026 ProtonVPN-Aether-Integration contributors
 * GPL-3.0
 */

using System.Security;
using ProtonVPN.Client.EventMessaging.Contracts;
using ProtonVPN.Client.Logic.Auth.Contracts;
using ProtonVPN.Client.Logic.Auth.Contracts.Enums;
using ProtonVPN.Client.Logic.Auth.Contracts.Messages;
using ProtonVPN.Client.Logic.Auth.Contracts.Models;

namespace ProtonVPN.Client.Logic.Auth;

/// <summary>
/// A <see cref="IUserAuthenticator"/> that is always logged in as a free user.
/// It performs no network calls and exposes no account data, so the app runs
/// 100% offline/free and jumps straight into the main UI.
/// </summary>
public class FreeUserAuthenticator : IUserAuthenticator
{
    private readonly IEventMessageSender _eventMessageSender;

    public FreeUserAuthenticator(IEventMessageSender eventMessageSender)
    {
        _eventMessageSender = eventMessageSender;
    }

    public AuthenticationStatus AuthenticationStatus => AuthenticationStatus.LoggedIn;

    public bool IsLoggedIn => true;

    public bool? IsAutoLogin => true;

    public bool IsTwoFactorAuthenticatorModeEnabled => false;

    public bool IsTwoFactorSecurityKeyModeEnabled => false;

    public bool HasAuthenticatedSessionData() => true;

    public Task<SsoAuthResult> StartSsoAuthAsync(string username)
        => Task.FromResult(SsoAuthResult.FromAuthResult(AuthResult.Fail("No-account mode: SSO is not available.")));

    public Task<AuthResult> CompleteSsoAuthAsync(string ssoResponseToken)
        => Task.FromResult(AuthResult.Fail("No-account mode: SSO is not available."));

    public Task<AuthResult> LoginUserAsync(string username, SecureString password)
        => Task.FromResult(AuthResult.Ok());

    public Task<AuthResult> SendTwoFactorCodeAsync(string code)
        => Task.FromResult(AuthResult.Fail("No-account mode: 2FA is not available."));

    public Task<AuthResult> AuthenticateWithSecurityKeyAsync()
        => Task.FromResult(AuthResult.Fail("No-account mode: security keys are not available."));

    public Task<AuthResult> AutoLoginUserAsync(bool isAppStartup)
    {
        // Fire the same event the real authenticator fires when it goes to
        // LoggedIn, so MainWindowViewNavigator.NavigateToDefaultAsync lands on
        // the main (free) window — not the login page. No backend is contacted.
        _eventMessageSender.Send(new AuthenticationStatusChanged(AuthenticationStatus.LoggedIn));
        return Task.FromResult(AuthResult.Ok());
    }

    public Task LogoutAsync(LogoutReason reason)
    {
        // No-account mode: logging out is a no-op — the user is always free.
        return Task.CompletedTask;
    }

    public void CancelAuth()
    {
    }
}
