using AgenticPitstop.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.IISIntegration;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
ApsSecrets.Apply(builder.Configuration);
// -----------------------------------------------------------------------------
// Authentication. Aps:AuthMode selects it: "Okta" (default) or "Windows".
//
// This is the swap the original comment anticipated. Both modes end at the same
// place: ApsIdentity reads the portal id from whichever claim the mode supplies,
// so the RBAC filter, every controller and every view are untouched. Windows
// stays registered rather than deleted, because a site that cannot reach Okta
// must be able to fall back by changing one configuration value.
// -----------------------------------------------------------------------------
var authMode = builder.Configuration["Aps:AuthMode"] ?? "Okta";
var useOkta = authMode.Equals("Okta", StringComparison.OrdinalIgnoreCase);

if (useOkta)
{
    var okta = builder.Configuration.GetSection("OktaOidc");

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = ApsIdentity.OidcScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.Cookie.Name = "APS.Session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

        // The sign-in cookie and the idle window expire together, so a user is
        // never holding a valid cookie the portal has already timed out.
        options.ExpireTimeSpan = TimeSpan.FromMinutes(
            builder.Configuration.GetValue<int?>("Aps:IdleTimeoutMinutes") ?? 20);
        options.SlidingExpiration = true;

        options.AccessDeniedPath = "/Home/Denied";
    })
    .AddOpenIdConnect(ApsIdentity.OidcScheme, options =>
    {
        // Configuration holds the whole discovery URL, which is what an Okta
        // administrator hands you. The handler wants the authority, which is the
        // same URL without the discovery suffix.
        options.Authority = (okta["MetadataAddress"] ?? string.Empty)
            .Replace("/.well-known/openid-configuration", string.Empty);

        options.ClientId = okta["ClientId"];
        options.ClientSecret = okta["ClientSecret"];

        options.ResponseType = OpenIdConnectResponseType.Code;
        options.ResponseMode = OpenIdConnectResponseMode.FormPost;
        options.UsePkce = true;

        // Okta's /v1/logout refuses a sign-out that carries no id_token_hint, and
        // the handler can only supply it from a saved token. ASP.NET chunks the
        // cookie if it grows past 4 KB, so a user in many groups still works.
        options.SaveTokens = true;
        options.GetClaimsFromUserInfoEndpoint = true;

        options.Scope.Clear();
        foreach (var scope in (okta["Scope"] ?? "openid profile email")
                 .Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            options.Scope.Add(scope);
        }

        options.CallbackPath = okta["CallbackPath"] ?? "/signin-oidc";
        options.SignedOutCallbackPath = okta["SignedOutCallbackPath"] ?? "/signout-callback-oidc";
        options.SignedOutRedirectUri = okta["PostLogoutRedirectUri"] ?? "/";

        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "name",
            RoleClaimType = "groups"
        };
    });
}
else
{
    // Negotiate covers Kestrel and HTTP.sys; the IIS handler covers IIS.
    builder.Services.AddAuthentication(NegotiateDefaults.AuthenticationScheme)
                    .AddNegotiate();

    builder.Services.Configure<IISServerOptions>(options =>
    {
        options.AutomaticAuthentication = true;
    });
}

builder.Services.AddAuthorization(options =>
{
    // Anonymous requests never reach a controller.
    options.FallbackPolicy = options.DefaultPolicy;
});

// Behind a TLS-terminating load balancer the handler must know the request was
// HTTPS, or it builds an http:// redirect_uri that Okta refuses and the
// correlation cookie is dropped. Harmless when there is no proxy.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// -----------------------------------------------------------------------------
// Application services
// -----------------------------------------------------------------------------
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ApsDb>();
builder.Services.AddScoped<ApsIdentity>();
builder.Services.AddScoped<ApsMetadata>();
builder.Services.AddScoped<ApsDetailRuntime>();
builder.Services.AddScoped<ApsFileRoots>();
builder.Services.AddApiConfigurator();
builder.Services.AddInflowArchive();
builder.Services.AddSopAnalyzer();

// The connectivity/API test on the Configuration screen calls the customer's
// endpoint from the server, never from the browser, so credentials in the
// configured headers are not exposed to the client.
builder.Services.AddHttpClient("ApsInflowProbe", client =>
{
    client.Timeout = TimeSpan.FromSeconds(20);
});

builder.Services.AddControllersWithViews(options =>
{
    // Anti-forgery on every non-GET request, including the AJAX save endpoints.
    options.Filters.Add(
        new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
});

builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "RequestVerificationToken";
    options.Cookie.Name = "APS.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

var app = builder.Build();

// First in the pipeline, so everything after it - PathBase, HTTPS redirection,
// the OIDC handler - sees the scheme the browser actually used.
app.UseForwardedHeaders();

// A path base lets one IIS site serve /customerabc, /customerxyz and so on as
// separate applications, each bound to its own database. It has to be registered
// before routing, or generated links will drop the prefix.
var pathBase = builder.Configuration["Aps:PathBase"];
if (!string.IsNullOrWhiteSpace(pathBase))
{
    // Under Kestrel the application also answers at the site root, where
    // PathBase is empty and the sign-in redirect_uri would be built without the
    // prefix - which Okta then refuses. Sending the root to the base gives the
    // portal one entry point. Under IIS as an application this never fires,
    // because IIS only routes the prefixed path to us.
    app.Use(async (context, next) =>
    {
        if (context.Request.Path == "/")
        {
            context.Response.Redirect(pathBase + "/");
            return;
        }

        await next();
    });

    app.UsePathBase(pathBase);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

// -----------------------------------------------------------------------------
// Security headers. Inline script is allowed only when it carries this request's
// nonce. Note that a browser ignores 'unsafe-inline' whenever a nonce is present,
// so the two must never be combined.
// -----------------------------------------------------------------------------
var isDevelopment = app.Environment.IsDevelopment();

app.Use(async (context, next) =>
{
    var nonce = context.ApsNonce();
    var headers = context.Response.Headers;

    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "SAMEORIGIN";
    headers["Referrer-Policy"] = "same-origin";

    // Browser Link and dotnet watch open a websocket back to the IDE on a
    // random localhost port. Widened for Development only.
    var connectSrc = isDevelopment
        ? "connect-src 'self' ws: wss: http://localhost:* https://localhost:*"
        : "connect-src 'self'";

    headers["Content-Security-Policy"] =
        "default-src 'self'; " +
        $"script-src 'self' 'nonce-{nonce}'; " +
        "style-src 'self' 'unsafe-inline'; " +
        "img-src 'self' data:; " +
        "font-src 'self'; " +
        connectSrc + "; " +
        "frame-ancestors 'self'; " +
        "base-uri 'self'; " +
        "form-action 'self'";

    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();
// The secret store failed at startup. Every screen needs the database, so the
// cause is shown rather than letting each one fail with its own SQL error.
app.Use(async (context, next) =>
{
    if (ApsSecrets.StartupError == null) { await next(); return; }

    context.Response.StatusCode = 503;
    context.Response.ContentType = "text/html; charset=utf-8";

    await context.Response.WriteAsync(
        "<!doctype html><html><head><meta charset='utf-8'>" +
        "<title>Agentic PITSTOP - configuration problem</title>" +
        "<link rel='stylesheet' href='" + context.Request.PathBase + "/css/site.css'></head>" +
        "<body><div style='max-width:760px;margin:12vh auto;padding:0 20px'>" +
        "<h1 style='margin:0 0 10px'>The portal cannot start</h1>" +
        "<p style='color:#667085'>Its database connection could not be loaded.</p>" +
        "<div class='callout callout-danger'>" +
        System.Net.WebUtility.HtmlEncode(ApsSecrets.StartupError) +
        "</div><p style='color:#667085;font-size:12px'>" +
        "Fix the cause above and restart the application.</p></div></body></html>");
});
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// After authorization, so only a recognised caller's activity is tracked.
app.UseApsIdleTimeout();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Inbox}/{action=Index}/{id?}");

app.Run();

public partial class Program { }
