using Curiosus.Hosting;
using Curiosus.Hosting.Web;
using Curiosus.Tools.Web.Controllers;
using NLog.Web;

var builder = CuriosusWebHost.CreateBuilder(args).AddCuriosusDefaults();
builder.AddCuriosusNLog(configureNLogExtensions: extensions => extensions.RegisterNLogWeb());

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.MapGet("/", () => "Hello World!");

Console.WriteLine(SiteError.Unauthorized.ToLocalizedString());

return await CuriosusApp.RunAsync(app);
