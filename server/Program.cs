using server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// removed: AddOpenApi() — redundant with Swagger

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddScoped<
    IFileStorageService,
    FileStorageService>();

builder.Services.AddScoped<FfmpegService>();
builder.Services.AddSingleton<WhisperService>();
builder.Services.AddScoped<GeminiService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

// HTTPS redirect only when NOT running in Docker
if (!app.Environment.IsEnvironment("Docker"))
{
    app.UseHttpsRedirection();
}

// Serve index.html at "/" and any other files in wwwroot
app.UseDefaultFiles();
// Map "/index" → "/index.html"
app.MapGet("/index", async context =>
{
    var path = Path.Combine(app.Environment.WebRootPath, "index.html");
    if (!File.Exists(path))
    {
        context.Response.StatusCode = 404;
        return;
    }
    context.Response.ContentType = "text/html";
    await context.Response.SendFileAsync(path);
});
app.UseStaticFiles();

app.UseCors("AllowFrontend");

app.UseAuthorization();

app.MapControllers();

app.Run();