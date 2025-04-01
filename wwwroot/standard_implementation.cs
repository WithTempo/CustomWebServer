var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseSession(); // Adds session handling automatically
app.MapPost("/store-data", async (HttpContext context) =>
{
    var body = await context.Request.ReadFromJsonAsync<Dictionary<string, string>>();
    context.Response.ContentType = "application/json";
    return Results.Json(new { message = "Received JSON", body });
});

app.Run("http://localhost:8080");



app.MapPost("/process", async (HttpRequest req) =>
{
    var body = await req.ReadFromJsonAsync<YourRequestModel>();
    var method = req.Method;
    var headers = req.Headers;
    return Results.Json(new { method, body, headers });
});



app.MapGet("/session", (HttpContext context) =>
{
    var sessionId = context.Session.GetString("sessionId") ?? Guid.NewGuid().ToString();
    context.Session.SetString("sessionId", sessionId);
    return Results.Json(new { sessionId });
});

app.UseExceptionHandler("/error"); // Centralized error handling route
app.Map("/error", () => Results.Problem("Something went wrong", statusCode: 500));

public class UserInput
{
    [Required]
    [StringLength(50)]
    public string Name { get; set; }
}

app.MapPost("/submit", (UserInput input) => Results.Ok($"Hello, {input.Name}!"))

var logger = app.Logger;
app.MapGet("/", () => {
    logger.LogInformation("Received GET request");
    return Results.Ok();
});

app.MapPost("/upload", async (HttpRequest req) =>
{
    var form = await req.ReadFormAsync();
    var file = form.Files.FirstOrDefault();
    var filePath = Path.Combine("uploads", file.FileName);
    using var stream = File.Create(filePath);
    await file.CopyToAsync(stream);
    return Results.Ok($"File uploaded to {filePath}");
});