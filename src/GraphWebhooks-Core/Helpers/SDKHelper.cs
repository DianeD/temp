/* 
*  Copyright (c) Microsoft. All rights reserved. Licensed under the MIT license. 
*  See LICENSE in the source repository root for complete license information. 
*/

using System.Net.Http.Headers;
using Microsoft.Graph;

namespace GraphWebhooks_Core.Helpers
{
    public class SDKHelper : ISDKHelper
    {
        private readonly ISampleAuthProvider authProvider;
        private GraphServiceClient graphClient = null;

        public SDKHelper(ISampleAuthProvider authProvider)
        {
            this.authProvider = authProvider;
        }
        
        // Get an authenticated Microsoft Graph Service client.
        // This sample sends the user ID to the sample auth provider, which uses the ID to interact with the token cache.
        public GraphServiceClient GetAuthenticatedClient(string userObjectId)
        {
            graphClient = new GraphServiceClient(
                new DelegateAuthenticationProvider(
                    async (requestMessage) =>
                    {
                        string accessToken = await authProvider.GetUserAccessTokenAsync(userObjectId);

                        // Append the access token to the request.
                        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

                        // This header identifies the sample in the Microsoft Graph service. If extracting this code for your project please remove.
                        requestMessage.Headers.Add("SampleID", "aspnetcore-webhooks-sample");
                    }));
            return graphClient;
        }
    }
    
    public interface ISDKHelper
    {
        GraphServiceClient GetAuthenticatedClient(string userObjectId);
    }
}