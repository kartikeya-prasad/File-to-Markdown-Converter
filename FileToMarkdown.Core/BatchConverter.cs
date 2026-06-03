namespace FileToMarkdown.Core;

/// <summary>
/// Runs a batch of conversions with bounded concurrency, reporting per-job progress.
/// Owns a single <see cref="ConversionService"/> (and therefore one persistent markitdown
/// worker and one OCR engine) for the whole batch.
/// </summary>
public sealed class BatchConverter : IAsyncDisposable
{
    private readonly ConversionOptions _options;
    private readonly ConversionService _service;

    public BatchConverter(ConversionOptions? options = null)
    {
        _options = options ?? new ConversionOptions();
        _service = new ConversionService(_options);
    }

    public ConversionOptions Options => _options;

    /// <summary>
    /// Converts every source into <paramref name="outputDir"/>. Reports a Running update as
    /// each job starts and a terminal update (Succeeded/Failed/Skipped) as it finishes.
    /// </summary>
    public async Task<IReadOnlyList<ConversionResult>> RunAsync(
        IReadOnlyList<string> sources,
        string outputDir,
        IProgress<JobProgress>? progress = null,
        CancellationToken ct = default)
    {
        using var gate = new SemaphoreSlim(_options.Concurrency);

        var tasks = sources.Select(async source =>
        {
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                progress?.Report(new JobProgress { Source = source, Status = ConversionStatus.Running });
                var result = await _service.ConvertAsync(source, outputDir, ct).ConfigureAwait(false);
                progress?.Report(new JobProgress
                {
                    Source = source,
                    Status = result.Status,
                    Route = result.Route,
                    OutputPath = result.OutputPath,
                    Error = result.Error,
                });
                return result;
            }
            finally
            {
                gate.Release();
            }
        }).ToList();

        return await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync() => _service.DisposeAsync();
}
