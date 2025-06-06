// ---------------------------------------------------------------------------- //
//                                                                              //
//   Copyright 2024 Finebits (https://finebits.com/)                            //
//                                                                              //
//   Licensed under the Apache License, Version 2.0 (the "License"),            //
//   you may not use this file except in compliance with the License.           //
//   You may obtain a copy of the License at                                    //
//                                                                              //
//       http://www.apache.org/licenses/LICENSE-2.0                             //
//                                                                              //
//   Unless required by applicable law or agreed to in writing, software        //
//   distributed under the License is distributed on an "AS IS" BASIS,          //
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.   //
//   See the License for the specific language governing permissions and        //
//   limitations under the License.                                             //
//                                                                              //
// ---------------------------------------------------------------------------- //

using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Finebits.Authorization.OAuth2.Abstractions;
using Finebits.Authorization.OAuth2.Types;

namespace Finebits.Authorization.OAuth2.Microsoft
{
    public partial class MicrosoftAuthClient : AuthorizationClient, IRefreshable, IProfileReader, IUserAvatarLoader
    {
        protected MicrosoftConfiguration Configuration => Config as MicrosoftConfiguration;

        public MicrosoftAuthClient(HttpClient httpClient, IAuthenticationBroker broker, MicrosoftConfiguration config)
            : base(httpClient, broker, config)
        { }

        protected override Task<Uri> GetAuthenticationEndpointAsync(string userId, object properties, CancellationToken cancellationToken)
        {
            Uri authorizationEndpoint = Configuration.AuthorizationUri;
            string clientId = Configuration.ClientId;
            string redirectUri = Uri.EscapeDataString(Configuration.RedirectUri.ToString());

            string endpoint = $"{authorizationEndpoint}?response_type=code&response_mode=query&redirect_uri={redirectUri}&client_id={clientId}";

            if (properties is AuthProperties props)
            {
                endpoint += $"&state={props.State}&code_challenge={props.CodeChallenge}&code_challenge_method={props.CodeChallengeMethod}";
            }

            string scope = Configuration.GetScope();
            if (!string.IsNullOrEmpty(scope))
            {
                scope = Uri.EscapeDataString(scope);
                endpoint += $"&scope={scope}";
            }

            if (!string.IsNullOrEmpty(userId))
            {
                endpoint += $"&login_hint={userId}";
            }

            if (Configuration.Prompt != MicrosoftAuthPrompt.None)
            {
                endpoint += $"&prompt={MicrosoftConfiguration.ConvertPromptToString(Configuration.Prompt)}";
            }

            return Task.FromResult(new Uri(endpoint));
        }

        public Task<AuthCredential> RefreshAsync(Credential credential, CancellationToken cancellationToken = default)
        {
            return new RefreshableClient(this).RefreshAsync(credential, cancellationToken);
        }

        public Task<IUserProfile> ReadProfileAsync(Credential credential, CancellationToken cancellationToken = default)
        {
            return new ProfileReader<MicrosoftProfileContent>(this)
            {
                UserProfileCreator = (content) => new MicrosoftUserProfile
                {
                    Id = content.Id,
                    Email = content.Mail,
                    DisplayName = content.DisplayName,
                    GivenName = content.GivenName,
                    Surname = content.Surname,
                    UserPrincipalName = content.UserPrincipalName,
                    PreferredLanguage = content.PreferredLanguage
                }
            }.ReadProfileAsync(credential, cancellationToken);
        }

        public async Task<Stream> LoadAvatarAsync(Credential credential, CancellationToken cancellationToken = default)
        {
            if (credential is null)
            {
                throw new ArgumentNullException(nameof(credential));
            }

            cancellationToken.ThrowIfCancellationRequested();

            return await DownloadFileAsync<MicrosoftEmptyContent>(
                 endpoint: Configuration.UserAvatarUri,
                 method: HttpMethod.Get,
                 credential: credential,
                 headers: null,
                 cancellationToken: cancellationToken).ConfigureAwait(false);
        }
    }
}
