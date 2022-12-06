using BlazorServerTestApp;
using BlazorServerTestApp.Data;
using Microsoft.Extensions.Caching.Memory;
using MonorailCss;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddMemoryCache();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton<WeatherForecastService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.MapGet("/styles/style.css", (IMemoryCache cache) =>
{
    var framework = new CssFramework();
    var style = framework.Process(Monorail.CssClassValues());
    return Results.Text(style, "text/css");
});

app.Run();