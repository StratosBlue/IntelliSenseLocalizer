﻿using System.Globalization;

using Cuture.Http;
using IntelliSenseLocalizer.Models;

namespace IntelliSenseLocalizer;

public class DefaultIntelliSenseItemWebPageDownloader : IIntelliSenseItemWebPageDownloader
{
    private readonly string _cacheRoot;
    private readonly string _locale;
    public const string NotFoundPageContent = "404NotFound";

    public DefaultIntelliSenseItemWebPageDownloader(CultureInfo cultureInfo, string cacheRoot)
    {
        _locale = cultureInfo.Name.ToLowerInvariant();

        _cacheRoot = cacheRoot;

        if (!Directory.Exists(cacheRoot))
        {
            Directory.CreateDirectory(cacheRoot);
        }
    }

    public async Task<string> DownloadAsync(IntelliSenseItemDescriptor memberDescriptor, bool ignoreCache, CancellationToken cancellationToken = default)
    {
        var queryKey = memberDescriptor.GetMicrosoftDocsQueryKey();
        var intelliSenseFile = memberDescriptor.IntelliSenseFileDescriptor;
        var frameworkMoniker = intelliSenseFile.OwnerPack.FrameworkMoniker;
        var cacheDriectory = Path.Combine(_cacheRoot, intelliSenseFile.OwnerPack.PackName, frameworkMoniker, _locale);
        var cacheFilePath = Path.Combine(cacheDriectory, $"{queryKey}.html");

        var url = $"https://docs.microsoft.com/{_locale}/dotnet/api/{queryKey}?view={frameworkMoniker}";

        if (!ignoreCache
            && File.Exists(cacheFilePath))
        {
            var existedHtml = await File.ReadAllTextAsync(cacheFilePath, cancellationToken);
            if (string.Equals(existedHtml, NotFoundPageContent, StringComparison.OrdinalIgnoreCase))
            {
                throw NotFoundException();
            }
            return existedHtml;
        }

        var response = await url.CreateHttpRequest()
                                .UseUserAgent("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/99.0.4844.74 Safari/537.36 Edg/99.0.1150.52")
                                .UseSystemProxy()
                                .AutoRedirection()
                                .WithCancellation(cancellationToken)
                                .TryGetAsStringAsync();

        if (response.Exception is not null)
        {
            throw response.Exception;
        }

        if (response.ResponseMessage?.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            await File.WriteAllTextAsync(cacheFilePath, NotFoundPageContent, cancellationToken);
            throw NotFoundException();
        }

        response.ResponseMessage!.EnsureSuccessStatusCode();

        var html = response.Data!;
        if (!Directory.Exists(cacheDriectory))
        {
            Directory.CreateDirectory(cacheDriectory);
        }
        await File.WriteAllTextAsync(cacheFilePath, html, cancellationToken);

        return html;

        MSOnlineDocNotFoundException NotFoundException()
        {
            return new MSOnlineDocNotFoundException($"{url}");
        }
    }
}