using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using OracleWebApplication.Models;
using OracleWebApplication.Services;

namespace OracleWebApplication.Pages.Account;

public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuditService _audit;

    public LoginModel(SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager, AuditService audit)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _audit = audit;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        // Already logged in — send straight to dashboard
        if (User.Identity?.IsAuthenticated == true)
        {
            Response.Redirect(returnUrl ?? "/Dashboard");
            return;
        }

        if (!string.IsNullOrEmpty(ErrorMessage))
        {
            ModelState.AddModelError(string.Empty, ErrorMessage);
        }

        ReturnUrl = returnUrl ?? Url.Content("~/");

        // Clear any existing external cookies
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= "/Dashboard";

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await _signInManager.PasswordSignInAsync(
            Input.Email, Input.Password, Input.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            var user = await _userManager.FindByEmailAsync(Input.Email);
            if (user is not null)
            {
                user.LastLoginUtc = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                await _audit.LogAsync("LoginSuccess", user.Id, user.Email,
                    user.ClientTenantId, HttpContext.Connection.RemoteIpAddress?.ToString(), null);
            }
            return LocalRedirect(returnUrl);
        }

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Account locked out. Please try again later.");
            return Page();
        }

        await _audit.LogAsync("LoginFailed", null, Input.Email,
            null, HttpContext.Connection.RemoteIpAddress?.ToString(), "Invalid login attempt");

        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return Page();
    }
}
