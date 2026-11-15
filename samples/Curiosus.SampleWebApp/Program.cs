using Curiosus.Hosting;
using Curiosus.Hosting.Web;
using Curiosus.Tools.Web.Controllers;

var builder = CuriosusWebHost.CreateBuilder(args).AddCuriosusDefaults();
builder.AddCuriosusNLog();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.MapGet("/", () => "Hello World!");

Console.WriteLine(SiteError.Unauthorized.ToLocalizedString());

return await CuriosusApp.RunAsync(app);
