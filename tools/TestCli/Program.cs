using FileToMarkdown.Core;

// Usage: TestCli <inputDir> <outputDir>
var inputDir = args.Length > 0 ? args[0] : "testdata";
var outputDir = args.Length > 1 ? args[1] : Path.Combine(inputDir, "out");

Console.WriteLine($"Runtime ready : {RuntimeLocator.IsRuntimeReady}");
Console.WriteLine($"python.exe    : {RuntimeLocator.PythonExe}");
Console.WriteLine($"worker.py     : {RuntimeLocator.WorkerScript}");
Console.WriteLine($"tessdata      : {RuntimeLocator.TessdataDir}");
Console.WriteLine();

var sources = Directory.EnumerateFiles(inputDir)
    .Where(FileRouter.IsSupported)
    .OrderBy(f => f)
    .ToList();

Console.WriteLine($"Converting {sources.Count} file(s) -> {outputDir}\n");

await using var batch = new BatchConverter(new ConversionOptions { Concurrency = 4 });
var progress = new Progress<JobProgress>(p =>
{
    if (p.Status == ConversionStatus.Running) return;
    var name = Path.GetFileName(p.Source);
    Console.WriteLine($"  [{p.Status,-9}] {name,-22} via {p.Route,-10} {p.Error}");
});

var results = await batch.RunAsync(sources, outputDir, progress);

Console.WriteLine("\n--- output previews ---");
foreach (var r in results.Where(r => r.OutputPath is not null && File.Exists(r.OutputPath)))
{
    var text = await File.ReadAllTextAsync(r.OutputPath!);
    var head = text.Length > 200 ? text[..200] + "..." : text;
    Console.WriteLine($"\n### {Path.GetFileName(r.OutputPath)}\n{head.Trim()}");
}

var ok = results.Count(r => r.Status == ConversionStatus.Succeeded);
Console.WriteLine($"\nDone: {ok}/{results.Count} succeeded.");
