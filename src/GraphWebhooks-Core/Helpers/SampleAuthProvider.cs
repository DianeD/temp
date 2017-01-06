/* 
*  Copyright (c) Microsoft. All rights reserved. Licensed under the MIT license. 
*  See LICENSE in the source repository root for complete license information. 
*/

using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace GraphWebhooks_Core.Helpers
{
    public class SampleAuthProvider : ISampleAuthProvider
    {
        private readonly IMemoryCache memoryCache;
        private SampleTokenCache tokenCache;

        // Properties used to get and manage an access token.
        private string authority = Startup.Authority;
        private string appId = Startup.AppId;
        private string appSecret = Startup.AppSecret;
        private string graphResourceId = Startup.GraphResourceId;
        private string callbackPath = Startup.CallbackPath;
        
        public SampleAuthProvider(IMemoryCache memoryCache)
        {
            this.memoryCache = memoryCache;
        }
        
        // Gets an access token. First tries to get the token from the token cache.
        public async Task<string> GetUserAccessTokenAsync(string userObjectId)
        {
            tokenCache = new SampleTokenCache(
                userObjectId,
                memoryCache);
            var cachedItems = tokenCache.ReadItems(); // see what's in the cache

            AuthenticationContext authContext = new AuthenticationContext(authority, tokenCache);
            try
            {
                AuthenticationResult authResult = await authContext.AcquireTokenSilentAsync(
                    graphResourceId,
                    new ClientCredential(appId, appSecret),
                    new UserIdentifier(userObjectId, UserIdentifierType.UniqueId));
                return authResult.AccessToken;
            }
            catch (AdalException e)
            {
                throw e;
            }
        }
    }

    public interface ISampleAuthProvider
    {
        Task<string> GetUserAccessTokenAsync(string userObjectId);
    }
}
