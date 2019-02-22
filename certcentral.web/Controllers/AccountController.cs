using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using certcentral.web.Account;

namespace certcentral.web.Controllers
{
    public class AccountController : Controller
    {
        private readonly AccountService _accountService;
        public AccountController(AccountService accountService)
        {
            _accountService = accountService;
        }

        [Authorize]
        public async Task<IActionResult> Index()
        {
            var user = await _accountService.GetUserInfo(User.Identity.Name);
            return View(model: user);
        }

        public IActionResult Register()
        {
            return View();
        }


        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel vm)
        {
            await _accountService.RegisterAsync(vm.ToUserInfo());

            var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
            identity.AddClaim(new Claim(ClaimTypes.Name, vm.Name));

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        public async Task<IActionResult> Unregister()
        {
            await _accountService.UnregisterAsync(User.Identity.Name);
            return Redirect("/signout");
        }

        [Authorize]
        public async Task<IActionResult> DeleteCert(string thumbprint)
        {
            var user = await _accountService.GetUserInfo(User.Identity.Name);
            if (user.CertFileNames.ContainsKey(thumbprint))
            {
                await _accountService.DeleteAsync(user.Login, thumbprint);
                user.CertFileNames.Remove(thumbprint);
                await _accountService.RegisterAsync(user);
            }
           // _cache.Remove("certsByUser");
            return Redirect("/account");
        }
    }
}