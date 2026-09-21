namespace Resonance.Core.Services.Implementations;

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Resonance.Core.Models;
using Resonance.Core.Services.Abstractions;

/// <summary>
///     Implementação de <see cref="IFingerprintService"/> utilizando o executável nativo do FFmpeg
///     com o muxer nativo 'chromaprint' em formato Base64. 100% local, zero dados transmitidos.
/// </summary>
public class FFmpegFingerprintService : IFingerprintService
{
    private readonly ILogger<FFmpegFingerprintService> _logger;
    private readonly string _ffmpegPath;

    public FFmpegFingerprintService(ILogger<FFmpegFingerprintService> logger, string ffmpegPath = "ffmpeg")
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ffmpegPath = string.IsNullOrWhiteSpace(ffmpegPath) ? "ffmpeg" : ffmpegPath;
    }

    /// <inheritdoc />
    public async Task<AcousticFingerprint?> GenerateFingerprintAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            _logger.LogWarning("Arquivo de áudio não encontrado ou caminho inválido: {FilePath}", filePath);
            return null;
        }

        Process? process = null;
        try
        {
            // 1. Obter duração da gravação via ATL (ou fallback)
            int durationSeconds = GetDurationSeconds(filePath);

            var startInfo = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            startInfo.ArgumentList.Add("-v");
            startInfo.ArgumentList.Add("error");
            startInfo.ArgumentList.Add("-nostdin");
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(filePath);
            startInfo.ArgumentList.Add("-t");
            startInfo.ArgumentList.Add("120");
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add("chromaprint");
            startInfo.ArgumentList.Add("-fp_format");
            startInfo.ArgumentList.Add("base64");
            startInfo.ArgumentList.Add("pipe:1");

            process = new Process { StartInfo = startInfo };
            if (!process.Start())
            {
                _logger.LogError("Falha ao iniciar processo do FFmpeg para {FilePath}", filePath);
                return null;
            }

            var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

            var hash = (await stdoutTask.ConfigureAwait(false)).Trim();
            var stderr = await stderrTask.ConfigureAwait(false);

            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(hash))
            {
                _logger.LogWarning("FFmpeg finalizou com código {ExitCode} para {FilePath}. Erro: {Stderr}",
                    process.ExitCode, filePath, stderr);
                return null;
            }

            // Fallback de duração mínima estimada se ATL não reportar
            if (durationSeconds <= 0)
            {
                durationSeconds = 120;
            }

            return new AcousticFingerprint(hash, durationSeconds);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Cálculo de fingerprint cancelado para {FilePath}", filePath);
            KillProcessSafely(process);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gerar fingerprint acústico para {FilePath}", filePath);
            KillProcessSafely(process);
            return null;
        }
        finally
        {
            process?.Dispose();
        }
    }

    private static int GetDurationSeconds(string filePath)
    {
        try
        {
            var track = new ATL.Track(filePath);
            if (track.Duration > 0)
            {
                return track.Duration;
            }
        }
        catch
        {
            // Ignora erro de leitura ATL e utiliza fallback
        }

        return 0;
    }

    private void KillProcessSafely(Process? process)
    {
        if (process == null) return;
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Falha ao finalizar processo do FFmpeg.");
        }
    }
}
