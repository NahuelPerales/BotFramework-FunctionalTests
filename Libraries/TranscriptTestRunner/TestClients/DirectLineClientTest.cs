using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Bot.Connector.DirectLine;
using Microsoft.Rest.TransientFaultHandling;

namespace TranscriptTestRunner.TestClients
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
#pragma warning disable SA1649 // File name should match first type name
    public class DirectLineClientTest : DirectLineClient
#pragma warning restore SA1649 // File name should match first type name
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DirectLineClientTest"/> class.
        /// </summary>
        /// <param name="secret">A.</param>
        /// <param name="httpClient">.</param>
        public DirectLineClientTest(string secret, HttpClient httpClient)
            : base(secret)
        {
            HttpClient = httpClient;
        }

        /// <inheritdoc/>
        public override HttpHeaderValueCollection<ProductInfoHeaderValue> UserAgent => base.UserAgent;

        /// <inheritdoc/>
        public override IEnumerable<HttpMessageHandler> HttpMessageHandlers => base.HttpMessageHandlers;

        /// <inheritdoc/>
        public override IConversations Conversations => base.Conversations;

        /// <inheritdoc/>
        public override ITokens Tokens => base.Tokens;

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return base.Equals(obj);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <inheritdoc/>
        public override void SetRetryPolicy(RetryPolicy retryPolicy)
        {
            base.SetRetryPolicy(retryPolicy);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return base.ToString();
        }

        /// <inheritdoc/>
        protected override DelegatingHandler CreateHttpHandlerPipeline(HttpClientHandler httpClientHandler, params DelegatingHandler[] handlers)
        {
            return base.CreateHttpHandlerPipeline(httpClientHandler, handlers);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }
    }
}
