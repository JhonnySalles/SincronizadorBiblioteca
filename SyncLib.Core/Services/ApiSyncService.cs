using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SyncLib.Core.Models.Api;

namespace SyncLib.Core.Services;

public class ApiSyncService
{
    private readonly HttpClient _httpClient;
    
    // Configurable base URL, defaults to localhost:8083 as seen in application.yml
    public string BaseUrl { get; set; } = "http://localhost:8083";

    public ApiSyncService()
    {
        _httpClient = new HttpClient();
    }

    public async Task<bool> IsApiOnlineAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{BaseUrl}/health");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<string> CheckFileStatusAsync(string filePath, string fileName, string mediaType)
    {
        try
        {
            if (string.Equals(mediaType, "Livro", StringComparison.OrdinalIgnoreCase))
            {
                return await CheckEpubStatusAsync(filePath, fileName);
            }
            else
            {
                return await CheckComicStatusAsync(filePath, fileName);
            }
        }
        catch
        {
            return "Erro";
        }
    }

    public async Task<string> CheckEpubStatusAsync(string filePath, string fileName)
    {
        try
        {
            // 1. Checa Book por filename
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                var res = await _httpClient.GetAsync($"{BaseUrl}/api/book/file-name?fileName={Uri.EscapeDataString(fileName)}");
                if (res.IsSuccessStatusCode)
                {
                    var existing = await res.Content.ReadFromJsonAsync<BookDto>();
                    if (existing != null && existing.Id.HasValue && existing.Id != Guid.Empty)
                    {
                        return "Atualizar";
                    }
                }
            }

            // 2. Extrai OPF e checa por título ou série/volume
            var (opfDto, _) = MetadataExtractor.ExtractOpf(filePath);
            if (opfDto != null)
            {
                if (!string.IsNullOrWhiteSpace(opfDto.Title))
                {
                    var res = await _httpClient.GetAsync($"{BaseUrl}/api/opf/search/title-exact?title={Uri.EscapeDataString(opfDto.Title)}");
                    if (res.IsSuccessStatusCode)
                    {
                        var list = await res.Content.ReadFromJsonAsync<List<OpfDto>>();
                        if (list != null && list.Count > 0)
                            return "Atualizar";
                    }
                }

                if (!string.IsNullOrWhiteSpace(opfDto.Series) && opfDto.Volume.HasValue)
                {
                    var res = await _httpClient.GetAsync($"{BaseUrl}/api/opf/search/serie-volume-language?serie={Uri.EscapeDataString(opfDto.Series)}&volume={opfDto.Volume.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}&language={Uri.EscapeDataString(opfDto.Language)}");
                    if (res.IsSuccessStatusCode)
                    {
                        var list = await res.Content.ReadFromJsonAsync<List<OpfDto>>();
                        if (list != null && list.Count > 0)
                            return "Atualizar";
                    }
                }
            }

            return "Novo";
        }
        catch
        {
            return "Erro";
        }
    }

    public async Task<string> CheckComicStatusAsync(string filePath, string fileName)
    {
        try
        {
            // 1. Checa Manga por filename
            if (!string.IsNullOrWhiteSpace(fileName))
            {
                var res = await _httpClient.GetAsync($"{BaseUrl}/api/manga/file-name?fileName={Uri.EscapeDataString(fileName)}");
                if (res.IsSuccessStatusCode)
                {
                    var existing = await res.Content.ReadFromJsonAsync<MangaDto>();
                    if (existing != null && existing.Id.HasValue && existing.Id != Guid.Empty)
                    {
                        return "Atualizar";
                    }
                }
            }

            // 2. Extrai ComicInfo e checa por comic, série/volume ou título/volume
            var (comicDto, _) = MetadataExtractor.ExtractComicInfo(filePath);
            if (comicDto != null)
            {
                if (!string.IsNullOrWhiteSpace(comicDto.Comic))
                {
                    var res = await _httpClient.GetAsync($"{BaseUrl}/api/comicinfo/search/comic?comic={Uri.EscapeDataString(comicDto.Comic)}");
                    if (res.IsSuccessStatusCode)
                    {
                        var list = await res.Content.ReadFromJsonAsync<List<ComicInfoDto>>();
                        if (list != null && list.Count > 0)
                            return "Atualizar";
                    }
                }

                if (!string.IsNullOrWhiteSpace(comicDto.Series) && comicDto.Volume.HasValue)
                {
                    var res = await _httpClient.GetAsync($"{BaseUrl}/api/comicinfo/search/serie-volume-language?serie={Uri.EscapeDataString(comicDto.Series)}&volume={comicDto.Volume.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}&language={Uri.EscapeDataString(comicDto.LanguageISO)}");
                    if (res.IsSuccessStatusCode)
                    {
                        var list = await res.Content.ReadFromJsonAsync<List<ComicInfoDto>>();
                        if (list != null && list.Count > 0)
                            return "Atualizar";
                    }
                }

                if (!string.IsNullOrWhiteSpace(comicDto.Title) && comicDto.Volume.HasValue)
                {
                    var res = await _httpClient.GetAsync($"{BaseUrl}/api/comicinfo/search/title-volume-language?title={Uri.EscapeDataString(comicDto.Title)}&volume={comicDto.Volume.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}&language={Uri.EscapeDataString(comicDto.LanguageISO)}");
                    if (res.IsSuccessStatusCode)
                    {
                        var list = await res.Content.ReadFromJsonAsync<List<ComicInfoDto>>();
                        if (list != null && list.Count > 0)
                            return "Atualizar";
                    }
                }
            }

            return "Novo";
        }
        catch
        {
            return "Erro";
        }
    }

    public async Task SyncEpubAsync(string filePath, string fileName, string language)
    {
        var (opfDto, rawContent) = MetadataExtractor.ExtractOpf(filePath);
        if (opfDto == null) return;
        
        if (string.IsNullOrEmpty(opfDto.Language)) opfDto.Language = language;
        
        Guid? opfId = await ResolveOpfAsync(opfDto);
        if (opfId.HasValue && !string.IsNullOrEmpty(rawContent))
        {
            await SendRawDataAsync(opfId.Value, null, "OPF", "package.opf", rawContent);
        }

        var bookDto = new BookDto
        {
            Nome = opfDto.Title,
            FileName = fileName,
            Extension = ".epub",
            FileDate = DateTime.Now,
            Volume = opfDto.Volume,
            Serie = opfDto.Series,
            OpfId = opfId
        };
        await ResolveBookAsync(bookDto);
    }

    public async Task SyncComicAsync(string filePath, string fileName, string language)
    {
        var (comicDto, rawContent) = MetadataExtractor.ExtractComicInfo(filePath);
        if (comicDto == null) return;
        
        if (string.IsNullOrEmpty(comicDto.LanguageISO)) comicDto.LanguageISO = language;

        Guid? comicId = await ResolveComicInfoAsync(comicDto);
        if (comicId.HasValue && !string.IsNullOrEmpty(rawContent))
        {
            await SendRawDataAsync(null, comicId.Value, "ComicInfo", "ComicInfo.xml", rawContent);
        }

        var mangaDto = new MangaDto
        {
            Nome = comicDto.Title,
            FileName = fileName,
            Extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant(),
            FileDate = DateTime.Now,
            Volume = comicDto.Volume,
            Serie = comicDto.Series,
            ComicInfoId = comicId
        };
        await ResolveMangaAsync(mangaDto);
    }

    private async Task<Guid?> ResolveOpfAsync(OpfDto dto)
    {
        // 1. By title exact
        if (!string.IsNullOrWhiteSpace(dto.Title))
        {
            var res = await _httpClient.GetAsync($"{BaseUrl}/api/opf/search/title-exact?title={Uri.EscapeDataString(dto.Title)}");
            if (res.IsSuccessStatusCode)
            {
                var list = await res.Content.ReadFromJsonAsync<List<OpfDto>>();
                if (list != null && list.Count > 0)
                {
                    var existing = list.First();
                    dto.Id = existing.Id;
                    await _httpClient.PutAsJsonAsync($"{BaseUrl}/api/opf/{existing.Id}", dto);
                    return existing.Id;
                }
            }
        }

        // 2. By serie + volume + language
        if (!string.IsNullOrWhiteSpace(dto.Series) && dto.Volume.HasValue)
        {
            var res = await _httpClient.GetAsync($"{BaseUrl}/api/opf/search/serie-volume-language?serie={Uri.EscapeDataString(dto.Series)}&volume={dto.Volume.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}&language={Uri.EscapeDataString(dto.Language)}");
            if (res.IsSuccessStatusCode)
            {
                var list = await res.Content.ReadFromJsonAsync<List<OpfDto>>();
                if (list != null && list.Count > 0)
                {
                    var existing = list.First();
                    dto.Id = existing.Id;
                    await _httpClient.PutAsJsonAsync($"{BaseUrl}/api/opf/{existing.Id}", dto);
                    return existing.Id;
                }
            }
        }

        // Create new
        var postRes = await _httpClient.PostAsJsonAsync($"{BaseUrl}/api/opf", dto);
        if (postRes.IsSuccessStatusCode)
        {
            var created = await postRes.Content.ReadFromJsonAsync<OpfDto>();
            return created?.Id;
        }
        return null;
    }

    private async Task<Guid?> ResolveComicInfoAsync(ComicInfoDto dto)
    {
        // 1. By comic (filename)
        if (!string.IsNullOrWhiteSpace(dto.Comic))
        {
            var res = await _httpClient.GetAsync($"{BaseUrl}/api/comicinfo/search/comic?comic={Uri.EscapeDataString(dto.Comic)}");
            if (res.IsSuccessStatusCode)
            {
                var list = await res.Content.ReadFromJsonAsync<List<ComicInfoDto>>();
                if (list != null && list.Count > 0)
                {
                    var existing = list.First();
                    dto.Id = existing.Id;
                    await _httpClient.PutAsJsonAsync($"{BaseUrl}/api/comicinfo/{existing.Id}", dto);
                    return existing.Id;
                }
            }
        }

        // 2. By serie + volume + language
        if (!string.IsNullOrWhiteSpace(dto.Series) && dto.Volume.HasValue)
        {
            var res = await _httpClient.GetAsync($"{BaseUrl}/api/comicinfo/search/serie-volume-language?serie={Uri.EscapeDataString(dto.Series)}&volume={dto.Volume.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}&language={Uri.EscapeDataString(dto.LanguageISO)}");
            if (res.IsSuccessStatusCode)
            {
                var list = await res.Content.ReadFromJsonAsync<List<ComicInfoDto>>();
                if (list != null && list.Count > 0)
                {
                    var existing = list.First();
                    dto.Id = existing.Id;
                    await _httpClient.PutAsJsonAsync($"{BaseUrl}/api/comicinfo/{existing.Id}", dto);
                    return existing.Id;
                }
            }
        }

        // 3. By title + volume + language
        if (!string.IsNullOrWhiteSpace(dto.Title) && dto.Volume.HasValue)
        {
            var res = await _httpClient.GetAsync($"{BaseUrl}/api/comicinfo/search/title-volume-language?title={Uri.EscapeDataString(dto.Title)}&volume={dto.Volume.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}&language={Uri.EscapeDataString(dto.LanguageISO)}");
            if (res.IsSuccessStatusCode)
            {
                var list = await res.Content.ReadFromJsonAsync<List<ComicInfoDto>>();
                if (list != null && list.Count > 0)
                {
                    var existing = list.First();
                    dto.Id = existing.Id;
                    await _httpClient.PutAsJsonAsync($"{BaseUrl}/api/comicinfo/{existing.Id}", dto);
                    return existing.Id;
                }
            }
        }

        // Create new
        var postRes = await _httpClient.PostAsJsonAsync($"{BaseUrl}/api/comicinfo", dto);
        if (postRes.IsSuccessStatusCode)
        {
            var created = await postRes.Content.ReadFromJsonAsync<ComicInfoDto>();
            return created?.Id;
        }
        return null;
    }

    private async Task ResolveBookAsync(BookDto dto)
    {
        // 1. By filename
        if (!string.IsNullOrWhiteSpace(dto.FileName))
        {
            var res = await _httpClient.GetAsync($"{BaseUrl}/api/book/file-name?fileName={Uri.EscapeDataString(dto.FileName)}");
            if (res.IsSuccessStatusCode)
            {
                var existing = await res.Content.ReadFromJsonAsync<BookDto>();
                if (existing != null && existing.Id != Guid.Empty)
                {
                    dto.Id = existing.Id;
                    await _httpClient.PutAsJsonAsync($"{BaseUrl}/api/book/{existing.Id}", dto);
                    return;
                }
            }
        }

        // 2. By serie + volume
        if (!string.IsNullOrWhiteSpace(dto.Serie) && dto.Volume.HasValue)
        {
            var res = await _httpClient.GetAsync($"{BaseUrl}/api/book/search/serie-volume?serie={Uri.EscapeDataString(dto.Serie)}&volume={dto.Volume.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            if (res.IsSuccessStatusCode)
            {
                var list = await res.Content.ReadFromJsonAsync<List<BookDto>>();
                if (list != null && list.Count > 0)
                {
                    var existing = list.First();
                    dto.Id = existing.Id;
                    await _httpClient.PutAsJsonAsync($"{BaseUrl}/api/book/{existing.Id}", dto);
                    return;
                }
            }
        }

        // Create
        await _httpClient.PostAsJsonAsync($"{BaseUrl}/api/book", dto);
    }

    private async Task ResolveMangaAsync(MangaDto dto)
    {
        // 1. By filename
        if (!string.IsNullOrWhiteSpace(dto.FileName))
        {
            var res = await _httpClient.GetAsync($"{BaseUrl}/api/manga/file-name?fileName={Uri.EscapeDataString(dto.FileName)}");
            if (res.IsSuccessStatusCode)
            {
                var existing = await res.Content.ReadFromJsonAsync<MangaDto>();
                if (existing != null && existing.Id != Guid.Empty)
                {
                    dto.Id = existing.Id;
                    await _httpClient.PutAsJsonAsync($"{BaseUrl}/api/manga/{existing.Id}", dto);
                    return;
                }
            }
        }

        // 2. By serie + volume
        if (!string.IsNullOrWhiteSpace(dto.Serie) && dto.Volume.HasValue)
        {
            var res = await _httpClient.GetAsync($"{BaseUrl}/api/manga/search/serie-volume?serie={Uri.EscapeDataString(dto.Serie)}&volume={dto.Volume.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
            if (res.IsSuccessStatusCode)
            {
                var list = await res.Content.ReadFromJsonAsync<List<MangaDto>>();
                if (list != null && list.Count > 0)
                {
                    var existing = list.First();
                    dto.Id = existing.Id;
                    await _httpClient.PutAsJsonAsync($"{BaseUrl}/api/manga/{existing.Id}", dto);
                    return;
                }
            }
        }

        // Create
        await _httpClient.PostAsJsonAsync($"{BaseUrl}/api/manga", dto);
    }

    private async Task SendRawDataAsync(Guid? opfId, Guid? comicInfoId, string tipo, string fileName, string content)
    {
        var dataFile = new DataFileDto
        {
            OpfId = opfId,
            ComicInfoId = comicInfoId,
            Tipo = tipo,
            FileName = fileName,
            FileContent = content
        };
        await _httpClient.PostAsJsonAsync($"{BaseUrl}/api/data", dataFile);
    }
}
