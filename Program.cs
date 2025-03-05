using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Net;
using DocShare.Helpers;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ClientManager>();
builder.Services.AddSingleton<FileManager>(sp => {
    var logger = sp.GetRequiredService<ILogger<FileManager>>();
    return new FileManager(logger, new DirectoryInfo(Path.Combine(builder.Environment.WebRootPath, "files")));
});

builder.Services.ConfigureHttpJsonOptions(options => {
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});


builder.Services.AddRazorPages();

var app = builder.Build();

//if (!app.Environment.IsDevelopment())
//{
//    app.UseExceptionHandler("/Error");
//}


var logger = app.Services.GetRequiredService<ILogger<Program>>();
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        logger.LogError(exception, "Unhandled exception occurred");
        await context.Response.WriteAsJsonAsync(new { error = "Internal Server Error" });
    });
});



app.UseStaticFiles();

app.UseRouting();

app.MapRazorPages();

app.MapGet("/api/files", async (HttpContext http, [FromServices]FileManager fileStorage) =>
{
    var files = fileStorage.GetFileList();
    http.Response.ContentType = "application/json";
    await http.Response.WriteAsJsonAsync(files);
});

app.MapPost("/api/files", async ([FromServices] ILogger<Program> logger, HttpContext http, [FromServices]FileManager fileStorage, [FromServices]ClientManager clients, IFormFileCollection upload) =>
{
    try
    {   
        logger.LogInformation("Uploading {Count} files", upload.Count);

        foreach (var item in upload)
        {
            if (item.Length > 0)
            {
                var fileName = await fileStorage.AddFileAsync(item.FileName, item.OpenReadStream());
                await clients.BroadcastMessageAsync(new NotifyFileAdded(fileName));
            }
        }

        await http.Response.WriteAsJsonAsync(new { success = true });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to upload file");
        http.Response.StatusCode = 400;
        //return new JsonResult(new { error = ex.Message });
        await http.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
}).DisableAntiforgery();

app.MapGet("/api/notify", async (HttpContext http, CancellationToken cancellationToken, [FromServices]ClientManager clients) =>
{
    http.Response.ContentType = "text/event-stream";

    var writer = new StreamWriter(http.Response.Body);
    var id = clients.AddClient(writer);

    while (!cancellationToken.IsCancellationRequested)
    {
        // this notification is mostly for debugging purposes and used as a keep-alive
        await clients.SendMessageAsync(id, new NotifyStatus(clients.ClientCount));

        await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
    }

    clients.RemoveClient(id);
});


// launch the browser
if (!app.Environment.IsDevelopment())
{
    var port = Environment.GetEnvironmentVariable("PORT") ?? "5100";
    var nc = new NetConfig();
    var url = $"http://{nc.GetLocalIP()}:{port}";
    Console.WriteLine(url);
    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    app.Run(url);
}
else
{
    app.Run();

}



