using System;
using System.Net.Http;

namespace TranscriptTestRunner
{
    /// <summary>
    /// .
    /// </summary>
    /// <remarks>
    /// ..
    /// </remarks>
    public class HttpClientInvoker
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HttpClientInvoker"/> class.
        /// </summary>
        /// <param name="client">..</param>
        /// <param name="handler">.</param>
        public HttpClientInvoker(HttpClient client, HttpClientHandler handler)
        {
            HttpClient = client;
            HttpClientHandler = handler;
        }

        /// <summary>
        /// Gets.
        /// </summary>
        /// <value>
        /// .
        /// </value>
        public HttpClient HttpClient { get; }

        /// <summary>
        /// Gets.
        /// </summary>
        /// <value>
        /// .
        /// </value>
        public HttpClientHandler HttpClientHandler { get; }
    }
}
