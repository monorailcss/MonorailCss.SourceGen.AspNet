using Microsoft.Extensions.Caching.Memory;
using MonorailCss;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.MapGet("/styles/style.css", (IMemoryCache cache) =>
{
    var style = cache.GetOrCreate("monorail-style", _ =>
    {
        var framework = new CssFramework();
        return framework.Process(MvcTestApp.MonorailCss.CssClassValues());
    });

    return Results.Text(style, "text/css");
});

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();