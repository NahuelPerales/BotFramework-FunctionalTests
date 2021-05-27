// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TranscriptTestRunner;

public class TestFixture : IDisposable
{
    public TestFixture()
    {
        var cookieContainer = new CookieContainer();
        var handler = new HttpClientHandler()
        {
            AllowAutoRedirect = false,
            CookieContainer = cookieContainer
        };

        var clientBuilder = new ServiceCollection()
                .AddHttpClient(Options.DefaultName)
                .ConfigurePrimaryHttpMessageHandler(() =>
                {
                    return handler;
                })
                .Services.BuildServiceProvider();

        var httpClientFactory = clientBuilder.GetService<IHttpClientFactory>();

        var client = httpClientFactory.CreateClient();

        // We have a sign in url, which will produce multiple HTTP 302 for redirects
        // This will path 
        //      token service -> other services -> auth provider -> token service (post sign in)-> response with token
        // When we receive the post sign in redirect, we add the cookie passed in the session info
        // to test enhanced authentication. This in the scenarios happens by itself since browsers do this for us.
        HttpClientInvoker = new HttpClientInvoker(client, handler);
    }

    public HttpClientInvoker HttpClientInvoker { get; }

    public void Dispose()
    {
        HttpClientInvoker.HttpClient.Dispose();
    }
}
