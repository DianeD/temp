﻿/*
 *  Copyright (c) Microsoft. All rights reserved. Licensed under the MIT license.
 *  See LICENSE in the source repository root for complete license information.
 */

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Graph;
using GraphWebhooks_Core.Models;
using GraphWebhooks_Core.Helpers;
using Microsoft.Extensions.Caching.Memory;

namespace GraphWebhooks_Core.Controllers
{
    public class NotificationController : Controller
    {
        private readonly IMemoryCache _memoryCache;
        private readonly ISDKHelper _sdkHelper;

        public NotificationController(IMemoryCache memoryCache, ISDKHelper sdkHelper)
        {
            _memoryCache = memoryCache;
            _sdkHelper = sdkHelper;
        }

        public ActionResult LoadView()
        {
            return View("Notification");
        }

        // The notificationUrl endpoint that's registered with the webhook subscription.
        [HttpPost]
        public async Task<ActionResult> Listen()
        {

            // Validate the new subscription by sending the token back to Microsoft Graph.
            // This response is required for each subscription.
            var query = QueryHelpers.ParseQuery(Request.QueryString.ToString());
            if (query.ContainsKey("validationToken"))
            {                
                return Content(query["validationToken"], "plain/text");
            }

            // Parse the received notifications.
            else
            {
                try
                {
                    var notifications = new Dictionary<string, Notification>();
                    using (var inputStream = new System.IO.StreamReader(Request.Body))
                    {
                        JObject jsonObject = JObject.Parse(inputStream.ReadToEnd());
                        if (jsonObject != null)
                        {

                            // Notifications are sent in a 'value' array.
                            JArray value = JArray.Parse(jsonObject["value"].ToString());
                            foreach (var notification in value)
                            {
                                Notification current = JsonConvert.DeserializeObject<Notification>(notification.ToString());

                                // Check client state to verify the message is from Microsoft Graph. 
                                var subscriptionParams = _memoryCache.Get("subscriptionId_" + current.SubscriptionId) as Tuple<string, string>;

                                // Verify client state to comply with the recommended notification handling process.
                                if (current.ClientState == subscriptionParams?.Item1)
                                {
                                    // Just keep the latest notification for each resource.
                                    // No point pulling data more than once.
                                    notifications[current.Resource] = current;
                                }
                            }

                            if (notifications.Count > 0)
                            {
                                // Query for the changed messages. 
                                await GetChangedMessagesAsync(notifications.Values);
                            }
                        }
                    }
                }
                catch (Exception)
                {

                    // TODO: Handle the exception.
                    // Still return a 202 so the service doesn't resend the notification.
                }
                return new StatusCodeResult(202);
            }
        }

        // Get information about the changed messages and send to browser via SignalR.
        // A production application would typically queue a background job for reliability.
        private async Task GetChangedMessagesAsync(IEnumerable<Notification> notifications)
        {
            List<Message> messages = new List<Message>();
            foreach (var notification in notifications)
            {
                if (notification.ResourceData.ODataType != "#Microsoft.Graph.Message") continue;

                // Get the stored user object ID.
                var subscriptionParams = (Tuple<string, string>)_memoryCache.Get("subscriptionId_" + notification.SubscriptionId);
                string userObjectId = subscriptionParams.Item2;

                // Initialize the GraphServiceClient, using the user ID associated with the subscription.
                GraphServiceClient graphClient = _sdkHelper.GetAuthenticatedClient(userObjectId);
                MessageRequest request = new MessageRequest(graphClient.BaseUrl + "/" + notification.Resource, graphClient, null);
                try
                {
                    messages.Add(await request.GetAsync());
                }
                catch (Exception)
                {
                    continue;
                }
            }
            
            if (messages.Count > 0)
            {
                //NotificationService notificationService = new NotificationService();
                //notificationService.SendNotificationToClient(messages);
            }
        }
    }
}