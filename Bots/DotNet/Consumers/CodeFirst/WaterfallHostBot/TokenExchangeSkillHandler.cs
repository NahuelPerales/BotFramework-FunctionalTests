﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core.Skills;
using Microsoft.Bot.Builder.Skills;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;

namespace Microsoft.BotFrameworkFunctionalTests.WaterfallHostBot
{
    /// <summary>
    /// A <see cref="SkillHandler"/> specialized to support SSO Token exchanges.
    /// </summary>
    public class TokenExchangeSkillHandler : CloudSkillHandler
    {
        private const string WaterfallSkillBot = "WaterfallSkillBot";

        private readonly BotAdapter _adapter;
        private readonly SkillsConfiguration _skillsConfig;
        private readonly string _botId;
        private readonly string _connectionName;
        private readonly SkillConversationIdFactoryBase _conversationIdFactory;
        private readonly ILogger _logger;
        private readonly BotFrameworkAuthentication _botAuth;
        
        public TokenExchangeSkillHandler(
            BotAdapter adapter,
            IBot bot,
            IConfiguration configuration,
            SkillConversationIdFactoryBase conversationIdFactory,
            BotFrameworkAuthentication authConfig,
            SkillsConfiguration skillsConfig,
            ILogger logger = null)
            : base(adapter, bot, conversationIdFactory, authConfig, logger)
        {
            _adapter = adapter;
            _botAuth = authConfig;
            _conversationIdFactory = conversationIdFactory;
            _skillsConfig = skillsConfig ?? new SkillsConfiguration(configuration);
            _botId = configuration.GetSection(MicrosoftAppCredentials.MicrosoftAppIdKey)?.Value;
            _logger = logger;

            var settings = configuration.GetSection("Bot.Builder.Community.Components.TokenExchangeSkillHandler")?.Get<ComponentSettings>() ?? new ComponentSettings();
            _connectionName = settings.TokenExchangeConnectionName ?? configuration.GetSection("tokenExchangeConnectionName")?.Value;
        }

        protected override async Task<ResourceResponse> OnSendToConversationAsync(ClaimsIdentity claimsIdentity, string conversationId, Activity activity, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (await InterceptOAuthCards(claimsIdentity, activity).ConfigureAwait(false))
            {
                return new ResourceResponse(Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
            }

            return await base.OnSendToConversationAsync(claimsIdentity, conversationId, activity, cancellationToken).ConfigureAwait(false);
        }

        protected override async Task<ResourceResponse> OnReplyToActivityAsync(ClaimsIdentity claimsIdentity, string conversationId, string activityId, Activity activity, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (await InterceptOAuthCards(claimsIdentity, activity).ConfigureAwait(false))
            {
                return new ResourceResponse(Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture));
            }

            return await base.OnReplyToActivityAsync(claimsIdentity, conversationId, activityId, activity, cancellationToken).ConfigureAwait(false);
        }

        private BotFrameworkSkill GetCallingSkill(ClaimsIdentity claimsIdentity)
        {
            var appId = JwtTokenValidation.GetAppIdFromClaims(claimsIdentity.Claims);

            if (string.IsNullOrWhiteSpace(appId))
            {
                return null;
            }

            return _skillsConfig.Skills.Values.FirstOrDefault(s => string.Equals(s.AppId, appId, StringComparison.InvariantCultureIgnoreCase));
        }

        private async Task<bool> InterceptOAuthCards(ClaimsIdentity claimsIdentity, Activity activity)
        {
            var oauthCardAttachment = activity.Attachments?.FirstOrDefault(a => a?.ContentType == OAuthCard.ContentType);
            if (oauthCardAttachment != null)
            {
                var targetSkill = GetCallingSkill(claimsIdentity);
                if (targetSkill != null)
                {
                    var oauthCard = ((JObject)oauthCardAttachment.Content).ToObject<OAuthCard>();

                    if (!string.IsNullOrWhiteSpace(oauthCard?.TokenExchangeResource?.Uri))
                    {
                        using (var context = new TurnContext(_adapter, activity))
                        {
                            context.TurnState.Add<IIdentity>("BotIdentity", claimsIdentity);

                            // We need to know what connection name to use for the token exchange so we figure that out here
                            // var connectionName = targetSkill.Id.Contains(WaterfallSkillBot) ? _configuration.GetSection("SsoConnectionName").Value : _configuration.GetSection("SsoConnectionNameTeams").Value;

                            //if (string.IsNullOrEmpty(_connectionName))
                            //{
                            //    throw new ArgumentException("The connection name cannot be null.");
                            //}

                            // AAD token exchange
                            try
                            {
                                var tokenClient = await _botAuth.CreateUserTokenClientAsync(claimsIdentity, CancellationToken.None).ConfigureAwait(false);
                                var result = await tokenClient.ExchangeTokenAsync(
                                    activity.Recipient.Id,
                                    _connectionName,
                                    activity.ChannelId,
                                    new TokenExchangeRequest {Uri = oauthCard.TokenExchangeResource.Uri},
                                    CancellationToken.None).ConfigureAwait(false);

                                if (!string.IsNullOrEmpty(result?.Token))
                                {
                                    // If token above is null, then SSO has failed and hence we return false.
                                    // If not, send an invoke to the skill with the token. 
                                    return await SendTokenExchangeInvokeToSkillAsync(activity, oauthCard.TokenExchangeResource.Id, result.Token, oauthCard.ConnectionName, targetSkill, default).ConfigureAwait(false);
                                }
                            }
                            catch (Exception ex)
                            {
                                // Show oauth card if token exchange fails.
                                _logger.LogWarning("Unable to exchange token.", ex);
                                return false;
                            }

                            return false;
                        }
                    }
                }
            }

            return false;
        }

        private async Task<bool> SendTokenExchangeInvokeToSkillAsync(Activity incomingActivity, string id, string token, string connectionName, BotFrameworkSkill targetSkill, CancellationToken cancellationToken)
        {
            var activity = incomingActivity.CreateReply();
            activity.Type = ActivityTypes.Invoke;
            activity.Name = SignInConstants.TokenExchangeOperationName;
            activity.Value = new TokenExchangeInvokeRequest
            {
                Id = id,
                Token = token,
                ConnectionName = connectionName,
            };

            var skillConversationReference = await _conversationIdFactory.GetSkillConversationReferenceAsync(incomingActivity.Conversation.Id, cancellationToken).ConfigureAwait(false);
            activity.Conversation = skillConversationReference.ConversationReference.Conversation;

            //activity.ServiceUrl = skillConversationReference.ConversationReference.ServiceUrl;

            // route the activity to the skill
            using var client = _botAuth.CreateBotFrameworkClient();
            var response = await client.PostActivityAsync(_botId, targetSkill.AppId, targetSkill.SkillEndpoint, _skillsConfig.SkillHostEndpoint, incomingActivity.Conversation.Id, activity, cancellationToken);

            // Check response status: true if success, false if failure
            return response.Status >= 200 && response.Status <= 299;
        }

        ///// <summary>
        ///// Performs a token exchange operation such as for single sign-on.
        ///// </summary>
        ///// <param name="turnContext">Context for the current turn of conversation with the user.</param>
        ///// <param name="connectionName">Name of the auth connection to use.</param>
        ///// <param name="userId">The user id associated with the token..</param>
        ///// <param name="exchangeRequest">The exchange request details, either a token to exchange or a uri to exchange.</param>
        ///// <param name="cancellationToken">A cancellation token that can be used by other objects
        ///// or threads to receive notice of cancellation.</param>
        ///// <returns>If the task completes, the exchanged token is returned.</returns>
        //private Task<TokenResponse> ExchangeTokenAsync(ITurnContext turnContext, string connectionName, string userId, TokenExchangeRequest exchangeRequest, CancellationToken cancellationToken = default(CancellationToken))
        //{
        //    return ExchangeTokenAsync(turnContext, null, connectionName, userId, exchangeRequest, cancellationToken);
        //}

        ///// <summary>
        ///// Performs a token exchange operation such as for single sign-on.
        ///// </summary>
        ///// <param name="turnContext">Context for the current turn of conversation with the user.</param>
        ///// <param name="oAuthAppCredentials">AppCredentials for OAuth.</param>
        ///// <param name="connectionName">Name of the auth connection to use.</param>
        ///// <param name="userId">The user id associated with the token..</param>
        ///// <param name="exchangeRequest">The exchange request details, either a token to exchange or a uri to exchange.</param>
        ///// <param name="cancellationToken">A cancellation token that can be used by other objects
        ///// or threads to receive notice of cancellation.</param>
        ///// <returns>If the task completes, the exchanged token is returned.</returns>
        //private async Task<TokenResponse> ExchangeTokenAsync(ITurnContext turnContext, AppCredentials oAuthAppCredentials, string connectionName, string userId, TokenExchangeRequest exchangeRequest, CancellationToken cancellationToken = default)
        //{
        //    BotAssert.ContextNotNull(turnContext);

        //    if (string.IsNullOrWhiteSpace(connectionName))
        //    {
        //        throw new ArgumentException($"{nameof(connectionName)} is null or empty", nameof(connectionName));
        //    }

        //    if (string.IsNullOrWhiteSpace(userId))
        //    {
        //        throw new ArgumentException($"{nameof(userId)} is null or empty", nameof(userId));
        //    }

        //    if (exchangeRequest == null)
        //    {
        //        throw new ArgumentException($"{nameof(exchangeRequest)} is null or empty", nameof(exchangeRequest));
        //    }

        //    if (string.IsNullOrWhiteSpace(exchangeRequest.Token) && string.IsNullOrWhiteSpace(exchangeRequest.Uri))
        //    {
        //        throw new ArgumentException("Either a Token or Uri property is required on the TokenExchangeRequest", nameof(exchangeRequest));
        //    }

        //    var client = new OAuthClient(oAuthAppCredentials);

        //    //var client = await CreateOAuthApiClientAsync(turnContext, oAuthAppCredentials).ConfigureAwait(false);
        //    var result = await client.ExchangeAsyncAsync(userId, connectionName, turnContext.Activity.ChannelId, exchangeRequest, cancellationToken).ConfigureAwait(false);

        //    if (result is ErrorResponse errorResponse)
        //    {
        //        throw new InvalidOperationException($"Unable to exchange token: ({errorResponse?.Error?.Code}) {errorResponse?.Error?.Message}");
        //    }

        //    if (result is TokenResponse tokenResponse)
        //    {
        //        return tokenResponse;
        //    }

        //    throw new InvalidOperationException($"ExchangeAsyncAsync returned improper result: {result.GetType()}");
        //}
    }
}
