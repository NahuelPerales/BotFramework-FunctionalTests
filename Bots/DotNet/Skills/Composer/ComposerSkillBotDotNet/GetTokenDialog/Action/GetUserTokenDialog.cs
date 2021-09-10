using System;
using System.IO;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AdaptiveExpressions.Properties;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;

namespace GetTokenDialog.Action
{
    public class GetUserTokenDialog : Dialog
    {
        [JsonProperty("$kind")]
        public const string Kind = "GetUserTokenDialog";

        [JsonConstructor]
        public GetUserTokenDialog([CallerFilePath] string sourceFilePath = "", [CallerLineNumber] int sourceLineNumber = 0)
            : base()
        {
            // enable instances of this command as debug break point
            RegisterSourceLocation(sourceFilePath, sourceLineNumber);
        }

        [JsonProperty("resultProperty")]
        public StringExpression ResultProperty { get; set; }

        public async override Task<DialogTurnResult> BeginDialogAsync(DialogContext dc, object options = null, CancellationToken cancellationToken = default)
        {
            var connectionName = dc.Parent.State.GetValue<string>("settings.SsoConnectionName");

            var userTokenClient = dc.Context.TurnState.Get<UserTokenClient>();

            var token = await userTokenClient.GetUserTokenAsync(dc.Context.Activity.From.Id, connectionName, dc.Context.Activity.ChannelId, null, cancellationToken).ConfigureAwait(false);

            if (this.ResultProperty != null)
            {
                dc.State.SetValue(this.ResultProperty.GetValue(dc.State), token?.Token);
            }

            return await dc.EndDialogAsync(result: token, cancellationToken: cancellationToken);
        }
    }
}
