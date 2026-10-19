namespace TestBackend
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            var app = builder.Build();

            app.MapGet("/", () => "Hello from backend");

            app.Run("http://localhost:5000");
        }
    }
}