namespace TestBackend
{
    internal class Program
    {
        static void Main(string[] args)
        {
            int port = args.Length > 0 ? int.Parse(args[0]) : 5000;
            Console.Title = $"TestBackend {port}";

            var builder = WebApplication.CreateBuilder();
            var app = builder.Build();

            app.MapGet("/", () => $"Hello from backend on port {port}");

            app.MapPost("/echo", async (HttpRequest request) =>
            {
                using var reader = new StreamReader(request.Body);
                return await reader.ReadToEndAsync();
            });

            app.Run($"http://localhost:{port}");
        }
    }
}
