/* 
*  Copyright (c) Microsoft. All rights reserved. Licensed under the MIT license. 
*  See LICENSE in the source repository root for complete license information. 
*/

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using GraphWebhooks_Core.Helpers;

namespace GraphWebhooks_Core
{
    public class Startup
    {
        public static string AppId;
        public static string AppSecret;
        public static string Authority;
        public static string GraphResourceId;
        public static string CallbackPath;
        public static IMemoryCache Cache;

        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true);

            if (env.IsDevelopment())
            {
                // For more details on using the user secret store see http://go.microsoft.com/fwlink/?LinkID=532709
                builder.AddUserSecrets();
            }
            builder.AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.
            services.AddMvc();

            // Add session services.
            // This sample uses an in-memory cache. Production apps will typically use some method of persistent storage.
            services.AddMemoryCache();
            services.AddSession();
            services.AddAuthentication(
                SharedOptions => SharedOptions.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme);

            // Add the sample's SampleAuthProvider and SDKHelper implementations.
            services.AddSingleton<ISampleAuthProvider, SampleAuthProvider>();
            services.AddTransient<ISDKHelper, SDKHelper>();

            //Add all SignalR related services to IoC.
            //services.AddSignalR(options => options.Hubs.EnableDetailedErrors = true);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory, IMemoryCache cache)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

            // Configure session middleware.
            app.UseSession();

            // Populate Azure AD configuration values.
            Authority = Configuration["Authentication:AzureAd:AADInstance"] + "Common";
            AppId = Configuration["Authentication:AzureAd:AppId"];
            AppSecret = Configuration["Authentication:AzureAd:AppSecret"];
            GraphResourceId = Configuration["Authentication:AzureAd:GraphResourceId"];
            CallbackPath = Configuration["Authentication:AzureAd:CallbackPath"];

            // Configure the OWIN pipeline to use cookie auth.
            app.UseCookieAuthentication(new CookieAuthenticationOptions
            {
                AutomaticAuthenticate = true,
            });
            var defaults = new OpenIdConnectOptions();

            app.UseOpenIdConnectAuthentication(new OpenIdConnectOptions()
            {
                ClientId = AppId,
                ClientSecret = AppSecret,
                Authority = Authority,
                CallbackPath = CallbackPath,
                ResponseType = OpenIdConnectResponseType.CodeIdToken,
                GetClaimsFromUserInfoEndpoint = false,
                TokenValidationParameters = new TokenValidationParameters
                {
                    // Instead of using the default validation (validating against a single issuer value, as we do in line of business apps),
                    // we inject our own multitenant validation logic
                    ValidateIssuer = false,

                    // If the app is meant to be accessed by entire organizations, add your issuer validation logic here.
                    //IssuerValidator = (issuer, securityToken, validationParameters) => {
                    //    if (myIssuerValidationLogic(issuer)) return issuer;
                    //}
                },
                Events = new OpenIdConnectEvents
                {
                    OnAuthorizationCodeReceived = async (context) =>
                    {
                        await GetOnAuthorizationCodeReceived(context, cache);
                    },
                    OnAuthenticationFailed = OnAuthenticationFailed,
                    //OnTicketReceived = (context) =>
                    //{
                    //    // If your authentication logic is based on users then add your logic here
                    //    return Task.FromResult(0);
                    //},
                    // If your application needs to do authenticate single users, add your user validation below for the tenants you want to support.
                    // (e.g. any tenant, Microsoft Account + specific list of Azure AD, single Azure AD, just Microsoft Account)
                    //OnTokenValidated = (context) =>
                    //{
                    //    return myUserValidationLogic(context.Ticket.Principal);
                    //}
                }
            });

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });

            //app.UseSignalR();
        }

        // Acquire a Token for Microsoft Graph and cache it using ADAL. 
        private async Task GetOnAuthorizationCodeReceived(AuthorizationCodeReceivedContext context, IMemoryCache cache)
        {
            string userObjectId = (context.Ticket.Principal.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier"))?.Value;
            ClientCredential clientCred = new ClientCredential(AppId, AppSecret);
            AuthenticationContext authContext = new AuthenticationContext(Authority, new SampleTokenCache(userObjectId, cache));
            AuthenticationResult authResult = await authContext.AcquireTokenByAuthorizationCodeAsync(
                context.ProtocolMessage.Code, new Uri(context.Properties.Items[OpenIdConnectDefaults.RedirectUriForCodePropertiesKey]), clientCred, GraphResourceId);

            // Notify the OIDC middleware that we already took care of code redemption.
            context.HandleCodeRedemption(authResult.AccessToken, authResult.IdToken);
        }

        // Handle sign-in errors differently than generic errors.
        private Task OnAuthenticationFailed(AuthenticationFailedContext context)
        {
            context.HandleResponse();
            context.Response.Redirect("Home/Error?message=" + context.Exception.Message);
            return Task.FromResult(0);
        }
    }
}
