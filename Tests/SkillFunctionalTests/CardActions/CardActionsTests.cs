// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Bot.Connector;
using Microsoft.Bot.Connector.DirectLine;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SkillFunctionalTests.Common;
using TranscriptTestRunner;
using TranscriptTestRunner.Authentication;
using TranscriptTestRunner.TestClients;
using TranscriptTestRunner.XUnit;
using Xunit;
using Xunit.Abstractions;

namespace SkillFunctionalTests.CardActions
{
    [Trait("TestCategory", "CardActions")]
    public class CardActionsTests : ScriptTestBase, IClassFixture<TestFixture>
    {
        private readonly string _testScriptsFolder = Directory.GetCurrentDirectory() + @"/CardActions/TestScripts";
        private readonly TestFixture _testFixture;

        public CardActionsTests(ITestOutputHelper output, TestFixture testFixture)
            : base(output)
        {
            _testFixture = testFixture;
        }

        public static IEnumerable<object[]> TestCases()
        {
            var channelIds = new List<string> { Channels.Directline };
            
            var deliverModes = new List<string>
            {
                DeliveryModes.Normal,
                DeliveryModes.ExpectReplies
            };

            var hostBots = new List<HostBot>
            {
                HostBot.WaterfallHostBotDotNet,
                HostBot.WaterfallHostBotJS,
                HostBot.WaterfallHostBotPython,

                // TODO: Enable this when the port to composer is ready
                //HostBot.ComposerHostBotDotNet
            };

            var targetSkills = new List<string>
            {
                SkillBotNames.WaterfallSkillBotDotNet,
                SkillBotNames.WaterfallSkillBotJS,
                SkillBotNames.WaterfallSkillBotPython,

                // TODO: Enable this when the port to composer is ready
                //SkillBotNames.ComposerSkillBotDotNet
            };

            var scripts = new List<string>
            {
                "BotAction.json",
                "TaskModule.json",
                "SubmitAction.json",
                "Hero.json",
                "Thumbnail.json",
                "Receipt.json",
                "SignIn.json",
                "Carousel.json",
                "List.json",
                "O365.json",
                "Animation.json",
                "Audio.json",
                "Video.json"
            };

            var testCaseBuilder = new TestCaseBuilder();

            // This local function is used to exclude ExpectReplies, O365 and WaterfallSkillBotPython test cases
            static bool ShouldExclude(TestCase testCase)
            {
                if (testCase.Script == "O365.json")
                {
                    // BUG: O365 fails with ExpectReplies for WaterfallSkillBotPython (remove when https://github.com/microsoft/BotFramework-FunctionalTests/issues/328 is fixed).
                    if (testCase.TargetSkill == SkillBotNames.WaterfallSkillBotPython && testCase.DeliveryMode == DeliveryModes.ExpectReplies)
                    {
                        return true;
                    }
                }

                return false;
            }

            var testCases = testCaseBuilder.BuildTestCases(channelIds, deliverModes, hostBots, targetSkills, scripts, ShouldExclude);
            foreach (var testCase in testCases)
            {
                yield return testCase;
            }
        }

        [Theory]
        [MemberData(nameof(TestCases))]
        public async Task RunTestCases(TestCaseDataObject testData)
        {
            //var testCase = testData.GetObject<TestCase>();
            //var tokenInfo = await GetDirectLineTokenAsync(TestClientOptions[testCase.HostBot].DirectLineSecret).ConfigureAwait(false);
            //var dlClient = new DirectLineClientTest(tokenInfo.Token, _testFixture.HttpClientInvoker.HttpClient);

            //await dlClient.HttpClient.GetAsync("https://jsonplaceholder.typicode.com/todos/1");
            //await dlClient.HttpClient.GetAsync("https://jsonplaceholder.typicode.com/todos/1");
            //await dlClient.HttpClient.GetAsync("https://jsonplaceholder.typicode.com/todos/1");

            var testCase = testData.GetObject<TestCase>();
            Logger.LogInformation(JsonConvert.SerializeObject(testCase, Formatting.Indented));

            var options = TestClientOptions[testCase.HostBot];
            var runner = new XUnitTestRunner(new TestClientFactory(testCase.ChannelId, options, Logger, _testFixture.HttpClientInvoker).GetTestClient(), TestRequestTimeout, Logger);

            var testParams = new Dictionary<string, string>
            {
                { "DeliveryMode", testCase.DeliveryMode },
                { "TargetSkill", testCase.TargetSkill }
            };

            await runner.RunTestAsync(Path.Combine(_testScriptsFolder, "WaterfallGreeting.json"), testParams);
            await runner.RunTestAsync(Path.Combine(_testScriptsFolder, testCase.Script), testParams);
        }

        private async Task<TokenInfo> GetDirectLineTokenAsync(string secret)
        {
            //using var client = new HttpClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://directline.botframework.com/v3/directline/tokens/generate");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", secret);
            request.Content = new StringContent(
                JsonConvert.SerializeObject(new
                {
                    User = new { Id = $"TestUser-{Guid.NewGuid()}" },
                    TrustedOrigins = new[] { $"https://botframework.test.com/{Guid.NewGuid()}" }
                }), Encoding.UTF8,
                "application/json");

            using var response = await _testFixture.HttpClientInvoker.HttpClient.SendAsync(request).ConfigureAwait(false);
            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch
            {
                // Log headers and body to help troubleshoot issues (the exception itself will be handled upstream).
                var sb = new StringBuilder();
                sb.AppendLine($"Failed to get a directline token (response status was: {response.StatusCode})");
                sb.AppendLine("Response headers:");
                sb.AppendLine(JsonConvert.SerializeObject(response.Headers, Formatting.Indented));
                sb.AppendLine("Response body:");
                sb.AppendLine(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
                throw;
            }

            // Extract token from response
            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            var tokenInfo = JsonConvert.DeserializeObject<TokenInfo>(body);
            if (string.IsNullOrWhiteSpace(tokenInfo?.Token))
            {
                throw new InvalidOperationException("Failed to acquire directLine token");
            }

            return tokenInfo;
        }
    }
}
