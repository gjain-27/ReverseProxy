namespace Proxy
{
    public static class Log
    {
        public static bool RequestsEnabled { get; set; } = true;

        public static void Request(string message)
        {
            if (RequestsEnabled)
            {
                Console.WriteLine(message);
            }
        }

        public static void Info(string message)
        {
            Console.WriteLine(message);
        }
    }
}
