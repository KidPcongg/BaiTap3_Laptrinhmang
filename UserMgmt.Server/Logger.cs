namespace UserMgmt.Server;

/// <summary>Ghi log ra console và ra tệp logs/server-yyyyMMdd.log, mỗi dòng có mốc thời gian. An toàn đa luồng.</summary>
public static class Logger
{
    private static readonly object Gate = new();
    private static readonly string LogDir = Path.Combine(AppContext.BaseDirectory, "logs");

    public static void Info(string message) => Write("INFO", message);
    public static void Warn(string message) => Write("WARN", message);
    public static void Error(string message) => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level,-5}] {message}";
        lock (Gate)
        {
            Console.ForegroundColor = level switch { "WARN" => ConsoleColor.Yellow, "ERROR" => ConsoleColor.Red, _ => ConsoleColor.Gray };
            Console.WriteLine(line);
            Console.ResetColor();
            try
            {
                Directory.CreateDirectory(LogDir);
                File.AppendAllText(Path.Combine(LogDir, $"server-{DateTime.Now:yyyyMMdd}.log"), line + Environment.NewLine);
            }
            catch (IOException) { /* không để lỗi ghi tệp làm sập server */ }
        }
    }
}
