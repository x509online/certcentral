using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Newtonsoft.Json;
using certcentral.web.Account;
using certcentral.web.Storage;

namespace certcentral.web
{
    public class Startup
    {
        private const string DefaultAuthScheme = "GitHub";
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            this.Configuration = configuration;
        }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<StorageOptions>(o => Configuration.Bind("Storage", o));

            services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddSingleton<AccountService>();
            services.AddSingleton<StorageProvider>();
            services.AddAuthentication(sharedOptions =>
            {
                sharedOptions.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                sharedOptions.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                sharedOptions.DefaultChallengeScheme = DefaultAuthScheme;
            })
            .AddOAuth(DefaultAuthScheme, options =>
            {
                Configuration.Bind("Auth", options);
                options.Events = new OAuthEvents
                {
                    //TODO: HANDLE THIS IN A SERVICE
                    OnCreatingTicket = async context =>
                    {
                        //TODO: EXCEPTION HANDLING
                        var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                        request.Headers.Authorization = new AuthenticationHeaderValue("token", context.AccessToken);

                        var response = await context.Backchannel.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, context.HttpContext.RequestAborted);
                        response.EnsureSuccessStatusCode();
                        var userInfo = JsonConvert.DeserializeObject<UserInfo>(await response.Content.ReadAsStringAsync());

                        var cIdentity = context.Principal?.Identity as ClaimsIdentity;
                        cIdentity.AddClaim(new Claim(ClaimTypes.Name, userInfo.Login));
                    },
                    OnTicketReceived = async context =>
                    {
                        var serviceProvider = services.BuildServiceProvider();
                        var accountService = serviceProvider.GetRequiredService<AccountService>();

                        var cIdentity = context.Principal?.Identity as ClaimsIdentity;

                        if (!(await accountService.IsRegisteredAsync(cIdentity.Name)))
                        {
                            //TODO: CREATE A SHARED ROUTES REPO
                            context.ReturnUri = "/account/register";
                        }
                    }
                };
            })
            .AddCookie(options => options.Cookie.Path = "/");
            services.AddMvc();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            app.Map("/signout", (Action<IApplicationBuilder>)(builder =>
            {
                var signoutEndpoint = this.Configuration.GetSection("Auth")?.GetValue<string>("SignOutEndPoint");
                if (!string.IsNullOrEmpty(signoutEndpoint))
                {
                    builder.Run(async context =>
                    {
                        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                        context.Response.Redirect(location: signoutEndpoint);
                    });
                }
            }));
            app.UseStaticFiles();
            app.UseAuthentication();
            app.UseMvcWithDefaultRoute();
        }
    }
}
