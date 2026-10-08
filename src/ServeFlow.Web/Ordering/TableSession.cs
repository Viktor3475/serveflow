using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using ServeFlow.Application.Ordering;

namespace ServeFlow.Web.Ordering;

public sealed class TableSession(IDataProtectionProvider protection, IHostEnvironment environment)
{
    private readonly ITimeLimitedDataProtector _protector = protection.CreateProtector("ServeFlow.TableSession.v1").ToTimeLimitedDataProtector();
    private string CookieName => environment.IsDevelopment() ? "ServeFlow.Table" : "__Host-ServeFlow.Table";
    public TableAccess? Read(HttpContext context)
    {
        if (!context.Request.Cookies.TryGetValue(CookieName, out var cookie) || cookie.Length > 2048) return null;
        try { return JsonSerializer.Deserialize<TableAccess>(_protector.Unprotect(cookie)); }
        catch (CryptographicException) { return null; }
        catch (JsonException) { return null; }
    }
    public void Issue(HttpContext context, TableAccess access) => context.Response.Cookies.Append(CookieName,
        _protector.Protect(JsonSerializer.Serialize(access), TimeSpan.FromHours(2)), new CookieOptions
        { HttpOnly = true, Secure = !environment.IsDevelopment() || context.Request.IsHttps, SameSite = SameSiteMode.Strict,
            Path = "/", MaxAge = TimeSpan.FromHours(2), IsEssential = true });
}
