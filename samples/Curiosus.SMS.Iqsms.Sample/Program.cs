using System.CommandLine;
using Curiosus.Hosting;
using Curiosus.SMS;
using Curiosus.SMS.Iqsms;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// App that sends a test message via iqsms.ru: a tool on Curiosus.Hosting with typed command line options.
var phoneOption = new Option<string>("--phone", "-p")
{
    Description = "Phone number to send the test message to",
    Required = true,
};
var configOption = new Option<string?>("--config", "-c")
{
    Description = "Directory with config.yml or a single configuration file",
};
var rootCommand = new RootCommand("Sends a test SMS via iqsms.ru") { phoneOption, configOption };

rootCommand.SetAction(async (parseResult, cancellationToken) =>
{
    var builder = CuriosusHost.CreateBuilder(args).AddCuriosusDefaults();
    builder.AddCuriosusNLog();

    // binds the "Iqsms" section and validates it when the host starts: an empty login or password stops the app
    builder.Services.AddIqsmsSender();

    var phoneNumber = parseResult.GetValue(phoneOption)!;

    return await CuriosusApp.RunAsync(builder.Build(), async (services, ct) =>
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("Curiosus.SMS.Iqsms.Sample");
        var sender = services.GetRequiredService<ISmsSender>();

        var result = await sender.SendSmsAsync(phoneNumber, "Hello iqsms!", ct);
        if (result.IsSuccess)
        {
            logger.LogInformation("We have successfully sent test message!");
            return CuriosusExitCodes.Success;
        }

        logger.LogError("We have some problems with sending test message: \"{Error}\", Content: \"{Content}\"",
            result.Errors.Select(x => $"code: {x.Code}, description: {x.Description}"),
            result.Body.ResponseJson);
        return 10;
    }, cancellationToken);
});

return await rootCommand.Parse(args).InvokeAsync();
