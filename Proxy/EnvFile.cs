namespace Proxy
{
    public static class EnvFile
    {
        public static Dictionary<string, string> Parse(string content)
        {
            Dictionary<string, string> values = new Dictionary<string, string>();

            foreach (string rawLine in content.Split('\n'))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                int equals = line.IndexOf('=');
                if (equals < 0)
                {
                    continue;
                }

                string key = line.Substring(0, equals).Trim();
                if (key.Length == 0)
                {
                    continue;
                }

                values[key] = line.Substring(equals + 1).Trim();
            }

            return values;
        }

        public static string? Load()
        {
            DirectoryInfo? directory = new DirectoryInfo(Directory.GetCurrentDirectory());

            while (directory != null)
            {
                string path = Path.Combine(directory.FullName, ".env");
                if (File.Exists(path))
                {
                    foreach (var pair in Parse(File.ReadAllText(path)))
                    {
                        Environment.SetEnvironmentVariable(pair.Key, pair.Value);
                    }

                    return directory.FullName;
                }

                directory = directory.Parent;
            }

            return null;
        }
    }
}
