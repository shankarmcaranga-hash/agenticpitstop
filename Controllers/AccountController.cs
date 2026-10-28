using AgenticPitstop.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgenticPitstop.Controllers;

/// <summary>
/// Sign-in and sign-out for Okta.
///
/// Deliberately not an ApsController: it runs before a portal user is known, so
/// it has no database, no ApsIdentity and no [ApsAuthorize]. The global
/// FallbackPolicy would otherwise challenge the sign-in endpoint itself.
/// </summary>
[AllowAnonymous]
public sealed class AccountController : Controller
{
    /// <summary>
    /// Most users never reach this: the fallback policy challenges them on the
    /// page they asked for. It exists for an explicit "sign in" link and for the
    /// return from the Session expired page.
    /// </summary>
    [HttpGet]
    public IActionResult Login(string returnUrl = null)
    {
        // Only a local path is honoured, so a crafted link cannot bounce a
        // freshly signed-in user to another site.
        var target = Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Action("Index", "Inbox");

        return Challenge(new AuthenticationProperties { RedirectUri = target },
                         ApsIdentity.OidcScheme);
    }

    /// <summary>
    /// Both schemes. Clearing only the portal cookie would leave the Okta
    /// session alive, and the next request would sign straight back in - which
    /// looks to the user as though Sign out did nothing.
    /// </summary>
    [HttpGet]
    public IActionResult Logout()
    {
        return SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            CookieAuthenticationDefaults.AuthenticationScheme,
            ApsIdentity.OidcScheme);
    }
}