/*
 *  Copyright (c) Microsoft. All rights reserved. Licensed under the MIT license.
 *  See LICENSE in the source repository root for complete license information.
 */

 using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Graph;
using GraphWebhooks_Core.Helpers;

namespace GraphWebhooks_Core.Controllers
{

    [Authorize]
    [ValidateAntiForgeryToken]
    public class SubscriptionController : Controller
    {
        private readonly IMemoryCache memoryCache;
        private readonly ISDKHelper sdkHelper;

        public SubscriptionController(IMemoryCache memoryCache, ISDKHelper sdkHelper)
        {
            this.memoryCache = memoryCache;
            this.sdkHelper = sdkHelper;
        }

        // Create a subscription.
        // https://graph.microsoft.io/en-us/docs/api-reference/v1.0/resources/webhooks
        public async Task<IActionResult> Create()
        {

            // Initialize the GraphServiceClient.
            string userObjectID = (User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier"))?.Value;
            GraphServiceClient graphClient = sdkHelper.GetAuthenticatedClient(userObjectID);
            
            Subscription newSubscription = new Subscription();
            string state = Guid.NewGuid().ToString();
            try
            {

                // Send the `POST /subscriptions` request.
                newSubscription = await graphClient.Subscriptions.Request().AddAsync(new Subscription
                {
                    Resource = "me/mailFolders('Inbox')/messages",
                    ChangeType = "created",
                    NotificationUrl = "https://6b6b3cf8.ngrok.io/notification/listen",
                    ClientState = state,
                    ExpirationDateTime = DateTime.UtcNow + new TimeSpan(0, 0, 4230, 0)
                });

                // Verify client state.
                if (newSubscription.ClientState != state)
                {
                    ViewBag.Message = "Warning: Mismatched client state.";
                }

                // Store the subscription ID to correlate a notification to the corresponding subscription.
                else
                {

                    // This sample temporarily stores the current subscription ID, client state, and user object ID.
                    // The NotificationController, which is not authenticated, uses this info to validate the subscription and get an access token keyed from the subscription ID.
                    // Production apps will typically use some method of persistent storage.
                    memoryCache.Set("subscriptionId_" + newSubscription.Id,
                        Tuple.Create(newSubscription.ClientState, HttpContext.User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value),
                        new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromHours(24)));
                }
            }
            catch (Exception e)
            {
                ViewBag.Message = BuildErrorMessage(e); 
                return View("Error", e);
            }
            return View("Subscription", newSubscription);
        }

        // Delete a subscription.
        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {

            // Initialize the GraphServiceClient.
            string userObjectID = (User.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier"))?.Value;
            GraphServiceClient graphClient = sdkHelper.GetAuthenticatedClient(userObjectID);
            try
            {

                // Send the `DELETE /subscriptions/{id}` request.
                await graphClient.Subscriptions[id].Request().DeleteAsync();
                ViewBag.Message = $"Deleted subscription {id}";

                memoryCache.Remove("subscriptionId_" + id);
            }
            catch (Exception e)
            {
                ViewBag.Message = BuildErrorMessage(e);
                return View("Error", e);
            }
            return View("Subscription");
        }

        private string BuildErrorMessage(Exception e)
        {
            string message = e.Message;
            if (e is AdalSilentTokenAcquisitionException) message = "Unable to get tokens. You may need to sign in again.";
            else if (e is ServiceException)
            {
                ServiceException se = e as ServiceException;
                message = $"{se.Error.Code}: {se.Error.Message}";
            }
            return message;
        }
    }
}
