namespace TestBackend
{
    internal class Program
    {
        static void Main(string[] args)
        {
            int port = args.Length > 0 ? int.Parse(args[0]) : 5000;
            Console.Title = $"TestBackend {port}";

            var builder = WebApplication.CreateBuilder();
            builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
            var app = builder.Build();

            app.MapGet("/", () => $"Hello from backend on port {port}");

            app.MapGet("/headers", (HttpRequest request) => $"X-Forwarded-For: {request.Headers["X-Forwarded-For"]}\nX-Forwarded-Proto: {request.Headers["X-Forwarded-Proto"]}\n");

            app.MapGet("/time", (HttpResponse response) =>
            {
                response.Headers.CacheControl = "public, max-age=10";
                return DateTime.Now.ToString("HH:mm:ss.fff");
            });

            app.MapGet("/private", (HttpResponse response) =>
            {
                response.Headers.CacheControl = "no-store";
                return DateTime.Now.ToString("HH:mm:ss.fff");
            });

            app.MapGet("/bench", (HttpResponse response) =>
            {
                response.Headers.CacheControl = "public, max-age=60";
                return "ok";
            });

            app.MapPost("/echo", async (HttpRequest request) =>
            {
                using var reader = new StreamReader(request.Body);
                return await reader.ReadToEndAsync();
            });

            app.Run($"http://localhost:{port}");
        }
    }
}
