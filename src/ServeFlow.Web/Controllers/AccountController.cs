using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServeFlow.Application.Accounts;
using ServeFlow.Web.Models;

namespace ServeFlow.Web.Controllers;

public sealed class AccountController(IAccountService accounts) : Controller
{
    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null) => View(new LoginModel { ReturnUrl = returnUrl });

    [AllowAnonymous, HttpPost, EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginModel model)
    {
        if (!ModelState.IsValid) { model.Password = string.Empty; return View(model); }
        if (!await accounts.SignInAsync(model.Email, model.Password, model.RememberMe))
        {
            ModelState.AddModelError(string.Empty, "Unable to sign in. Check your credentials or contact your administrator.");
            model.Password = string.Empty;
            return View(model);
        }
        return Url.IsLocalUrl(model.ReturnUrl) ? LocalRedirect(model.ReturnUrl!) : RedirectToAction("Index", "Home");
    }

    [Authorize, HttpPost]
    public async Task<IActionResult> Logout()
    {
        await accounts.SignOutAsync();
        return RedirectToAction("Login");
    }

    [Authorize, HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordModel());

    [Authorize, HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordModel model)
    {
        if (!ModelState.IsValid) return View(model);
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Forbid();
        var result = await accounts.ChangePasswordAsync(userId, model.CurrentPassword, model.NewPassword);
        if (result.Forbidden) return Forbid();
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error);
            return View(model);
        }
        TempData["Success"] = "Your password was changed. Other sessions will need to sign in again.";
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous, HttpGet]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }
}
