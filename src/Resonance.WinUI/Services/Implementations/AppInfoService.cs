using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Windows.ApplicationModel;
using Microsoft.Extensions.Logging;
using Resonance.Core.Services.Abstractions;

namespace Resonance.WinUI.Services.Implementations;

public class AppInfoService : IAppInfoService
{
    private readonly ILogger<AppInfoService> _logger;

    public AppInfoService(ILogger<AppInfoService> logger)
    {
        _logger = logger;
    }

    public string GetAppName()
    {
        try
        {
            return Package.Current.DisplayName;
        }
        catch
        {
            return "Resonance";
        }
    }

    public string GetAppVersion()
    {
        try
        {
            var version = Package.Current.Id.Version;
            return $"{version.Major}.{version.Minor}.{version.Build}";
        }
        catch
        {
            var asmVersion = Assembly.GetEntryAssembly()?.GetName().Version;
            return asmVersion != null ? $"{asmVersion.Major}.{asmVersion.Minor}.{asmVersion.Build}" : "2.3.0";
        }
    }


    private IReadOnlyList<string>? _cachedAvailableLanguages;
    private readonly System.Threading.SemaphoreSlim _languageCacheLock = new(1, 1);

    public async System.Threading.Tasks.Task<IReadOnlyList<string>> GetAvailableLanguagesAsync()
    {
        if (_cachedAvailableLanguages != null)
            return _cachedAvailableLanguages;

        await _languageCacheLock.WaitAsync();
        try
        {
            if (_cachedAvailableLanguages != null)
                return _cachedAvailableLanguages;

            // Discover supported languages by scanning package subdirectories for satellite assemblies.
            var satelliteName = Assembly.GetEntryAssembly()?.GetName().Name + ".resources.dll";
            var results = new List<string>();

            string? installPath = null;
            try
            {
                installPath = Package.Current.InstalledLocation.Path;
            }
            catch
            {
                installPath = AppContext.BaseDirectory;
            }

            _logger.LogDebug("Language scan: installFolder={Path}, satellite={Satellite}", installPath, satelliteName);

            if (System.IO.Directory.Exists(installPath))
            {
                foreach (var dir in System.IO.Directory.EnumerateDirectories(installPath))
                {
                    var dirName = System.IO.Path.GetFileName(dir);
                    try { _ = new CultureInfo(dirName); }
                    catch (CultureNotFoundException) { continue; }

                    var satellitePath = System.IO.Path.Combine(dir, satelliteName);
                    if (System.IO.File.Exists(satellitePath))
                    {
                        results.Add(dirName);
                    }
                }
            }

            _logger.LogDebug("Language scan: completed with {Count} languages", results.Count);
            _cachedAvailableLanguages = results;
            return _cachedAvailableLanguages;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to enumerate available languages.");
            return Array.Empty<string>();
        }
        finally
        {
            _languageCacheLock.Release();
        }
    }
    public async System.Threading.Tasks.Task InitializeAsync()
    {
        // Pre-cache languages
        await GetAvailableLanguagesAsync();
    }
}
