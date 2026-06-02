using SourceWorker.Dashboard.Components;
using SourceWorker.ApiClient;
using SourceWorker.UI.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents();
builder.Services.AddSourceWorkerApiClient(options =>
{
    builder.Configuration.GetSection("SourceWorkerApi").Bind(options);
    
    // Auto-discovery from environment variables (provided by AppHost)
    var envBaseUrl = Environment.GetEnvironmentVariable("SOURCEWORKER_API_BASE_URL");
    if (!string.IsNullOrEmpty(envBaseUrl))
    {
        options.BaseUrl = envBaseUrl;
    }

    var envToken = Environment.GetEnvironmentVariable("SOURCEWORKER_API_TOKEN");
    if (!string.IsNullOrEmpty(envToken))
    {
        options.BearerToken = envToken;
    }

    var envLaunchMode = Environment.GetEnvironmentVariable("SOURCEWORKER_LAUNCH_MODE");
    if (!string.IsNullOrEmpty(envLaunchMode))
    {
        options.LaunchMode = envLaunchMode;
    }
});
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<DashboardActionService>();
builder.Services.AddScoped<ISelectedProjectState, SelectedProjectState>();
builder.Services.AddScoped<IProjectFolderPicker, NoopProjectFolderPicker>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddAdditionalAssemblies(typeof(SourceWorker.UI.Pages.Home).Assembly);

app.Run();
