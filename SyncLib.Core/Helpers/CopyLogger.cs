using System;
using System.IO;
using System.Text;

namespace SyncLib.Core.Helpers;

public static class CopyLogger
{
    private static readonly object _lock = new();
    private const long MaxFileSizeBytes = 5 * 1024 * 1024; // 5 MB

    public static string LogFilePath => Path.Combine(AppContext.BaseDirectory, "copias_log.txt");

    public static void LogCopy(string sourcePath, string mediaType, string finalFileName, string destinationDir)
    {
        lock (_lock)
        {
            try
            {
                RotateLogIfOverSize();
                bool fileExists = File.Exists(LogFilePath);

                using var writer = new StreamWriter(LogFilePath, append: true, encoding: Encoding.UTF8);

                if (!fileExists)
                {
                    // Escreve o cabeçalho das colunas com espaçamento padronizado
                    string header = $"{Pad("Data e Hora", 20)} | {Pad("Tipo", 18)} | {Pad("Nome Final", 50)} | {Pad("Arquivo de Origem", 80)} | Pasta de Destino";
                    string separator = new string('-', header.Length + 20);

                    writer.WriteLine(header);
                    writer.WriteLine(separator);
                }

                string nowStr = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                string logLine = $"{Pad(nowStr, 20)} | {Pad(mediaType, 18)} | {Pad(finalFileName, 50)} | {Pad(sourcePath, 80)} | {destinationDir}";

                writer.WriteLine(logLine);
            }
            catch
            {
                // Silenciosamente ignora se houver concorrência imprevista de IO
            }
        }
    }

    private static void RotateLogIfOverSize()
    {
        if (File.Exists(LogFilePath))
        {
            var fileInfo = new FileInfo(LogFilePath);
            if (fileInfo.Length >= MaxFileSizeBytes)
            {
                string archivePath = Path.Combine(AppContext.BaseDirectory, $"copias_log_bkp_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.Move(LogFilePath, archivePath, overwrite: true);
            }
        }
    }

    private static string Pad(string value, int length)
    {
        if (string.IsNullOrEmpty(value)) return new string(' ', length);
        if (value.Length > length) return value.Substring(0, length - 3) + "...";
        return value.PadRight(length);
    }
}
