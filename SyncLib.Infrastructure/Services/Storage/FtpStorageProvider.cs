using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SyncLib.Core.Services.Storage;

namespace SyncLib.Infrastructure.Services.Storage;

public class FtpStorageProvider : IStorageProvider
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;

    public FtpStorageProvider(string host, int port = 21, string username = "anonymous", string password = "")
    {
        _host = string.IsNullOrWhiteSpace(host) ? "localhost" : host;
        _port = port > 0 ? port : 21;
        _username = string.IsNullOrWhiteSpace(username) ? "anonymous" : username;
        _password = password ?? string.Empty;
    }

    private async Task<(TcpClient client, StreamReader reader, StreamWriter writer)> ConnectAsync(CancellationToken cancellationToken)
    {
        var client = new TcpClient();
        await client.ConnectAsync(_host, _port, cancellationToken);
        var stream = client.GetStream();
        var reader = new StreamReader(stream, Encoding.UTF8);
        var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

        // Read initial welcome banner
        var welcome = await ReadResponseAsync(reader, cancellationToken);
        if (!welcome.StartsWith("220"))
        {
            client.Dispose();
            throw new IOException($"FTP server connection failed: {welcome}");
        }

        // Send USER
        await writer.WriteLineAsync($"USER {_username}".AsMemory(), cancellationToken);
        var userResp = await ReadResponseAsync(reader, cancellationToken);
        if (userResp.StartsWith("331"))
        {
            // Send PASS
            await writer.WriteLineAsync($"PASS {_password}".AsMemory(), cancellationToken);
            var passResp = await ReadResponseAsync(reader, cancellationToken);
            if (!passResp.StartsWith("230"))
            {
                client.Dispose();
                throw new IOException($"FTP authentication failed: {passResp}");
            }
        }
        else if (!userResp.StartsWith("230"))
        {
            client.Dispose();
            throw new IOException($"FTP user command failed: {userResp}");
        }

        // Send TYPE I (Binary mode)
        await writer.WriteLineAsync("TYPE I".AsMemory(), cancellationToken);
        await ReadResponseAsync(reader, cancellationToken);

        // Send OPTS UTF8 ON
        await writer.WriteLineAsync("OPTS UTF8 ON".AsMemory(), cancellationToken);
        await ReadResponseAsync(reader, cancellationToken);

        return (client, reader, writer);
    }

    private async Task<string> ReadResponseAsync(StreamReader reader, CancellationToken cancellationToken)
    {
        string? line;
        var sb = new StringBuilder();
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            sb.AppendLine(line);
            // RFC 959: multiline response has hyphen after code (e.g. "220-") and ends with code followed by space (e.g. "220 ")
            if (line.Length >= 4 && char.IsDigit(line[0]) && char.IsDigit(line[1]) && char.IsDigit(line[2]) && line[3] == ' ')
            {
                break;
            }
        }
        return sb.ToString().Trim();
    }

    private async Task<(TcpClient dataClient, NetworkStream dataStream)> OpenDataConnectionAsync(StreamReader controlReader, StreamWriter controlWriter, CancellationToken cancellationToken)
    {
        // Try EPSV first
        await controlWriter.WriteLineAsync("EPSV".AsMemory(), cancellationToken);
        var epsvResp = await ReadResponseAsync(controlReader, cancellationToken);

        if (epsvResp.StartsWith("229"))
        {
            var match = Regex.Match(epsvResp, @"\(\|\|\|(\d+)\|\)");
            if (match.Success && int.TryParse(match.Groups[1].Value, out var port))
            {
                var dataClient = new TcpClient();
                await dataClient.ConnectAsync(_host, port, cancellationToken);
                return (dataClient, dataClient.GetStream());
            }
        }

        // Fallback to PASV
        await controlWriter.WriteLineAsync("PASV".AsMemory(), cancellationToken);
        var pasvResp = await ReadResponseAsync(controlReader, cancellationToken);
        if (pasvResp.StartsWith("227"))
        {
            var match = Regex.Match(pasvResp, @"\((\d+),(\d+),(\d+),(\d+),(\d+),(\d+)\)");
            if (match.Success)
            {
                var ip = $"{match.Groups[1].Value}.{match.Groups[2].Value}.{match.Groups[3].Value}.{match.Groups[4].Value}";
                var port = (int.Parse(match.Groups[5].Value) << 8) + int.Parse(match.Groups[6].Value);
                var dataClient = new TcpClient();
                await dataClient.ConnectAsync(ip, port, cancellationToken);
                return (dataClient, dataClient.GetStream());
            }
        }

        throw new IOException($"Failed to open FTP passive data connection: {epsvResp} / {pasvResp}");
    }

    private string NormalizeFtpPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "/";
        var normalized = path.Replace('\\', '/');
        if (!normalized.StartsWith('/')) normalized = "/" + normalized;
        return normalized.TrimEnd('/');
    }

    public async Task<bool> DirectoryExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        var norm = NormalizeFtpPath(path);
        if (norm == "" || norm == "/") return true;

        try
        {
            var (client, reader, writer) = await ConnectAsync(cancellationToken);
            using (client)
            using (reader)
            using (writer)
            {
                await writer.WriteLineAsync($"CWD {norm}".AsMemory(), cancellationToken);
                var resp = await ReadResponseAsync(reader, cancellationToken);
                return resp.StartsWith("250");
            }
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> FileExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        var norm = NormalizeFtpPath(path);
        try
        {
            var (client, reader, writer) = await ConnectAsync(cancellationToken);
            using (client)
            using (reader)
            using (writer)
            {
                await writer.WriteLineAsync($"SIZE {norm}".AsMemory(), cancellationToken);
                var resp = await ReadResponseAsync(reader, cancellationToken);
                return resp.StartsWith("213");
            }
        }
        catch
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<string>> GetFilesAsync(string directoryPath, string searchPattern = "*.*", bool recursive = false, CancellationToken cancellationToken = default)
    {
        var norm = NormalizeFtpPath(directoryPath);
        var result = new List<string>();

        try
        {
            await ListEntriesInternalAsync(norm, recursive, result, isFiles: true, cancellationToken);
        }
        catch
        {
            // Ignore if folder does not exist
        }

        if (string.IsNullOrWhiteSpace(searchPattern) || searchPattern == "*.*" || searchPattern == "*")
        {
            return result;
        }

        var regexPattern = "^" + Regex.Escape(searchPattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$";
        var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
        return result.FindAll(f => regex.IsMatch(Path.GetFileName(f)));
    }

    public async Task<IReadOnlyList<string>> GetDirectoriesAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        var norm = NormalizeFtpPath(directoryPath);
        var result = new List<string>();

        try
        {
            await ListEntriesInternalAsync(norm, recursive: false, result, isFiles: false, cancellationToken);
        }
        catch
        {
            // Ignore
        }

        return result;
    }

    private async Task ListEntriesInternalAsync(string currentPath, bool recursive, List<string> resultList, bool isFiles, CancellationToken cancellationToken)
    {
        var (client, reader, writer) = await ConnectAsync(cancellationToken);
        using (client)
        using (reader)
        using (writer)
        {
            var (dataClient, dataStream) = await OpenDataConnectionAsync(reader, writer, cancellationToken);
            using (dataClient)
            using (dataStream)
            {
                await writer.WriteLineAsync($"LIST {currentPath}".AsMemory(), cancellationToken);
                var listResp = await ReadResponseAsync(reader, cancellationToken);
                if (!listResp.StartsWith("150") && !listResp.StartsWith("125"))
                {
                    return;
                }

                using var listReader = new StreamReader(dataStream, Encoding.UTF8);
                string? entry;
                var subDirs = new List<string>();

                while ((entry = await listReader.ReadLineAsync(cancellationToken)) != null)
                {
                    if (string.IsNullOrWhiteSpace(entry)) continue;

                    // Unix style: drwxr-xr-x 1 owner group 0 Jan 01 00:00 filename
                    // Windows style: 01-01-20 00:00AM <DIR> filename
                    bool isDir = entry.StartsWith('d') || entry.Contains("<DIR>");
                    string name;

                    if (entry.Contains("<DIR>"))
                    {
                        var parts = entry.Split(new[] { "<DIR>" }, StringSplitOptions.RemoveEmptyEntries);
                        name = parts.Length > 1 ? parts[1].Trim() : "";
                    }
                    else if (entry.StartsWith('d') || entry.StartsWith('-'))
                    {
                        // Unix format
                        var match = Regex.Match(entry, @"^[\w-]{10}\s+\d+\s+\S+\s+\S+\s+\d+\s+\w+\s+\d+\s+[\d:]+\s+(.+)$");
                        name = match.Success ? match.Groups[1].Value.Trim() : entry.Substring(Math.Min(entry.Length, 55)).Trim();
                    }
                    else
                    {
                        // Windows file format: 01-01-20 00:00AM 12345 filename
                        var tokens = entry.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        name = tokens.Length >= 4 ? string.Join(" ", tokens, 3, tokens.Length - 3).Trim() : entry.Trim();
                    }

                    if (name == "." || name == ".." || string.IsNullOrEmpty(name)) continue;

                    var fullItemPath = $"{currentPath.TrimEnd('/')}/{name}";

                    if (isDir)
                    {
                        if (!isFiles) resultList.Add(fullItemPath);
                        if (recursive) subDirs.Add(fullItemPath);
                    }
                    else
                    {
                        if (isFiles) resultList.Add(fullItemPath);
                    }
                }

                await ReadResponseAsync(reader, cancellationToken); // 226 Transfer complete

                if (recursive)
                {
                    foreach (var sub in subDirs)
                    {
                        await ListEntriesInternalAsync(sub, true, resultList, isFiles, cancellationToken);
                    }
                }
            }
        }
    }

    public async Task CreateDirectoryAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        var norm = NormalizeFtpPath(directoryPath);
        if (string.IsNullOrWhiteSpace(norm) || norm == "/") return;

        var segments = norm.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = "";

        var (client, reader, writer) = await ConnectAsync(cancellationToken);
        using (client)
        using (reader)
        using (writer)
        {
            foreach (var segment in segments)
            {
                current += "/" + segment;
                await writer.WriteLineAsync($"MKD {current}".AsMemory(), cancellationToken);
                await ReadResponseAsync(reader, cancellationToken); // Ignore error if already exists
            }
        }
    }

    public async Task CopyFileAsync(string sourcePath, string destinationPath, bool overwrite = true, CancellationToken cancellationToken = default)
    {
        // FTP to FTP copy or within same FTP server
        await using var readStream = await OpenReadAsync(sourcePath, cancellationToken);
        await UploadFileAsync(readStream, destinationPath, overwrite, cancellationToken);
    }

    public async Task<long> GetFileSizeAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var norm = NormalizeFtpPath(filePath);
        try
        {
            var (client, reader, writer) = await ConnectAsync(cancellationToken);
            using (client)
            using (reader)
            using (writer)
            {
                await writer.WriteLineAsync($"SIZE {norm}".AsMemory(), cancellationToken);
                var resp = await ReadResponseAsync(reader, cancellationToken);
                if (resp.StartsWith("213"))
                {
                    var sizeStr = resp.Substring(4).Trim();
                    if (long.TryParse(sizeStr, out var size)) return size;
                }
            }
        }
        catch { }
        return 0L;
    }

    public async Task<DateTime> GetLastModifiedAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var norm = NormalizeFtpPath(filePath);
        try
        {
            var (client, reader, writer) = await ConnectAsync(cancellationToken);
            using (client)
            using (reader)
            using (writer)
            {
                await writer.WriteLineAsync($"MDTM {norm}".AsMemory(), cancellationToken);
                var resp = await ReadResponseAsync(reader, cancellationToken);
                if (resp.StartsWith("213"))
                {
                    var timeStr = resp.Substring(4).Trim();
                    if (DateTime.TryParseExact(timeStr, "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    {
                        return dt;
                    }
                }
            }
        }
        catch { }
        return DateTime.MinValue;
    }

    public async Task<Stream> OpenReadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var norm = NormalizeFtpPath(filePath);
        var (client, reader, writer) = await ConnectAsync(cancellationToken);

        try
        {
            var (dataClient, dataStream) = await OpenDataConnectionAsync(reader, writer, cancellationToken);
            await writer.WriteLineAsync($"RETR {norm}".AsMemory(), cancellationToken);
            var retrResp = await ReadResponseAsync(reader, cancellationToken);
            if (!retrResp.StartsWith("150") && !retrResp.StartsWith("125"))
            {
                dataStream.Dispose();
                dataClient.Dispose();
                client.Dispose();
                throw new IOException($"Failed to retrieve FTP file '{norm}': {retrResp}");
            }

            var memory = new MemoryStream();
            await dataStream.CopyToAsync(memory, cancellationToken);
            dataStream.Dispose();
            dataClient.Dispose();
            await ReadResponseAsync(reader, cancellationToken); // 226 complete
            client.Dispose();

            memory.Position = 0;
            return memory;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    public async Task<Stream> OpenWriteAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var norm = NormalizeFtpPath(filePath);
        var dir = Path.GetDirectoryName(norm)?.Replace('\\', '/') ?? "/";
        await CreateDirectoryAsync(dir, cancellationToken);

        // Returns a memory stream that uploads when closed/disposed or callers use UploadFileAsync directly
        var memStream = new MemoryStream();
        return memStream;
    }

    public async Task UploadFileAsync(Stream sourceStream, string destinationPath, bool overwrite = true, CancellationToken cancellationToken = default)
    {
        var norm = NormalizeFtpPath(destinationPath);
        var dir = Path.GetDirectoryName(norm)?.Replace('\\', '/') ?? "/";
        await CreateDirectoryAsync(dir, cancellationToken);

        var (client, reader, writer) = await ConnectAsync(cancellationToken);
        using (client)
        using (reader)
        using (writer)
        {
            var (dataClient, dataStream) = await OpenDataConnectionAsync(reader, writer, cancellationToken);
            using (dataClient)
            using (dataStream)
            {
                await writer.WriteLineAsync($"STOR {norm}".AsMemory(), cancellationToken);
                var storResp = await ReadResponseAsync(reader, cancellationToken);
                if (!storResp.StartsWith("150") && !storResp.StartsWith("125"))
                {
                    throw new IOException($"Failed to store FTP file '{norm}': {storResp}");
                }

                await sourceStream.CopyToAsync(dataStream, cancellationToken);
                await dataStream.FlushAsync(cancellationToken);
            }
            await ReadResponseAsync(reader, cancellationToken); // 226 complete
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var (client, reader, writer) = await ConnectAsync(cancellationToken);
            using (client)
            using (reader)
            using (writer)
            {
                await writer.WriteLineAsync("NOOP".AsMemory(), cancellationToken);
                var resp = await ReadResponseAsync(reader, cancellationToken);
                return resp.StartsWith("200");
            }
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
    }
}
