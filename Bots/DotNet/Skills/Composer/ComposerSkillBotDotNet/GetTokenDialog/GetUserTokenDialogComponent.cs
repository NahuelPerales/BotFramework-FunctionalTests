﻿using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Dialogs.Declarative;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GetTokenDialog
{
    public class GetUserTokenDialogComponent : BotComponent
    {
        public override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // Anything that could be done in Startup.ConfigureServices can be done here.
            // In this case, the DownloadFileDialog needs to be added as a new DeclarativeType.
            services.AddSingleton<DeclarativeType>(sp => new DeclarativeType<Action.GetUserTokenDialog>(Action.GetUserTokenDialog.Kind));
        }
    }
}
