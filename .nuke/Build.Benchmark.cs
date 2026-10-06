using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Fanstatic.NUKE;

sealed partial class Build
{
    const int BenchmarkRunsPerScenario = 3;
    const int MaximumBenchmarkRegressionPercent = 25;
    const int BenchmarkProcessorCount = 2;

    static readonly int[] BenchmarkPostCounts = [1000, 2000, 4000];

    static readonly JsonSerializerOptions BenchmarkJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    [Parameter("Replace the checked-in benchmark baseline with the measured results")]
    readonly bool UpdateBenchmarkBaseline;

    AbsolutePath BenchmarkBaselineFile => RootDirectory / ".nuke" / "benchmark-baseline.json";
    AbsolutePath BenchmarkReportFile => RootDirectory / "artifacts" / "benchmark.json";
    AbsolutePath BenchmarkPublishDirectory => RootDirectory / ".publish" / "benchmark" / RuntimeIdentifier;

    Target Benchmark => td => td
        .DependsOn(Restore)
        .Produces(BenchmarkReportFile)
        .Executes(RunBenchmark);

    void RunBenchmark()
    {
        BenchmarkReportFile.DeleteFile();
        var tempDirectory = Path.Combine(Path.GetTempPath(), $"fanstatic-benchmark-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDirectory);

        List<BenchmarkMeasurement> measurements;
        try
        {
            PublishBenchmarkBinary();
            var executable = Path.Combine(BenchmarkPublishDirectory,
                OperatingSystem.IsWindows() ? "fanstatic.exe" : "fanstatic");
            if (!File.Exists(executable))
            {
                throw new FileNotFoundException("The published benchmark executable was not found.", executable);
            }

            measurements = [];
            foreach (var postCount in BenchmarkPostCounts)
            {
                foreach (var hasSidebar in new[] { false, true })
                {
                    var scenario = new BenchmarkScenario(postCount, hasSidebar);
                    measurements.Add(RunBenchmarkScenario(executable, tempDirectory, scenario));
                }
            }
        }
        finally
        {
            Directory.Delete(tempDirectory, recursive: true);
        }

        var baseline = UpdateBenchmarkBaseline
            ? WriteBenchmarkBaseline(measurements)
            : ReadBenchmarkBaseline();
        ValidateBenchmarkBaseline(baseline);

        var reportScenarios = measurements.Select(measurement =>
        {
            var baselineMedian = baseline.Scenarios
                .SingleOrDefault(entry => entry.Name == measurement.Name)?.MedianMilliseconds
                ?? throw new InvalidDataException($"Benchmark baseline is missing scenario '{measurement.Name}'.");
            if (baselineMedian <= 0)
            {
                throw new InvalidDataException($"Benchmark baseline for '{measurement.Name}' must be positive.");
            }

            var regressionPercent = (measurement.MedianMilliseconds - baselineMedian) / baselineMedian * 100;
            var passed = measurement.MedianMilliseconds <=
                         baselineMedian * (1 + MaximumBenchmarkRegressionPercent / 100d);

            Log.Information(
                "Benchmark {Scenario}: median {Median:F1} ms, baseline {Baseline:F1} ms, regression {Regression:F1}%, {Status}",
                measurement.Name, measurement.MedianMilliseconds, baselineMedian, regressionPercent,
                passed ? "PASS" : "FAIL");

            return new BenchmarkScenarioReport(measurement.Name, measurement.PostCount, measurement.HasSidebar,
                measurement.RunTimesMilliseconds, measurement.MedianMilliseconds, baselineMedian,
                regressionPercent, passed);
        }).ToArray();

        var report = new BenchmarkReport(DateTimeOffset.UtcNow, RuntimeIdentifier,
            RuntimeInformation.ProcessArchitecture.ToString(), Environment.Version.ToString(),
            BenchmarkProcessorCount, reportScenarios.All(scenario => scenario.Passed), reportScenarios);

        _ = BenchmarkReportFile.Parent.CreateDirectory();
        BenchmarkReportFile.WriteAllText(JsonSerializer.Serialize(report, BenchmarkJsonOptions));

        if (!report.Passed)
        {
            throw new InvalidOperationException(
                $"Benchmark regression exceeded {MaximumBenchmarkRegressionPercent}%; see '{BenchmarkReportFile}'.");
        }
    }

    void PublishBenchmarkBinary()
    {
        BenchmarkPublishDirectory.DeleteDirectory();

        _ = DotNetPublish(settings => settings
            .SetNoLogo(true)
            .SetProject(Solution.Fanstatic)
            .SetConfiguration(ConfigurationOptions.Release)
            .SetOutput(BenchmarkPublishDirectory)
            .SetRuntime(RuntimeIdentifier)
            .SetSelfContained(false)
            .SetPublishSingleFile(false)
            .SetPublishTrimmed(false)
        );
    }

    BenchmarkMeasurement RunBenchmarkScenario(string executable, string tempDirectory, BenchmarkScenario scenario)
    {
        var siteDirectory = Path.Combine(tempDirectory, scenario.Name);
        CreateBenchmarkSite(siteDirectory, scenario);

        List<double> runTimes = [];
        for (var run = 1; run <= BenchmarkRunsPerScenario; run++)
        {
            var outputDirectory = Path.Combine(tempDirectory, "output", $"{scenario.Name}-{run}");
            var elapsedMilliseconds = RunBenchmarkBuild(executable, siteDirectory, outputDirectory);
            runTimes.Add(elapsedMilliseconds);
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }

        var medianMilliseconds = runTimes.Order().ElementAt(BenchmarkRunsPerScenario / 2);
        return new BenchmarkMeasurement(scenario.Name, scenario.PostCount, scenario.HasSidebar,
            runTimes, medianMilliseconds);
    }

    static double RunBenchmarkBuild(string executable, string siteDirectory, string outputDirectory)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            WorkingDirectory = siteDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.Environment["DOTNET_PROCESSOR_COUNT"] =
            BenchmarkProcessorCount.ToString(CultureInfo.InvariantCulture);
        startInfo.ArgumentList.Add("build");
        startInfo.ArgumentList.Add("--source");
        startInfo.ArgumentList.Add(siteDirectory);
        startInfo.ArgumentList.Add("--output");
        startInfo.ArgumentList.Add(outputDirectory);

        var stopwatch = Stopwatch.StartNew();
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start benchmark executable '{executable}'.");
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        stopwatch.Stop();

        var standardOutput = standardOutputTask.GetAwaiter().GetResult();
        var standardError = standardErrorTask.GetAwaiter().GetResult();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Benchmark build failed with exit code {process.ExitCode}.{Environment.NewLine}" +
                $"{standardOutput}{Environment.NewLine}{standardError}");
        }

        return stopwatch.Elapsed.TotalMilliseconds;
    }

    static void CreateBenchmarkSite(string siteDirectory, BenchmarkScenario scenario)
    {
        var contentDirectory = Path.Combine(siteDirectory, "content", "blog");
        var themeDirectory = Path.Combine(siteDirectory, "themes", "benchmark", "_default");
        Directory.CreateDirectory(contentDirectory);
        Directory.CreateDirectory(themeDirectory);

        File.WriteAllText(Path.Combine(siteDirectory, "fanstatic.yaml"), """
            title: Fanstatic benchmark
            baseUrl: https://example.invalid/
            theme: benchmark
            kindOutputFormats:
              home:
                - html
              section:
                - html
              single:
                - html
              taxonomy:
                - html
              term:
                - html
            """);

        File.WriteAllText(Path.Combine(themeDirectory, "single.html"), "<article>{{ page.ContentPreRendered }}</article>");
        File.WriteAllText(Path.Combine(themeDirectory, "list.html"),
            scenario.HasSidebar
                ? """
                  <main>{{ page.ContentPreRendered }}</main>
                  <aside>
                  {% for recent in site.RegularPagesByDate reversed limit: 10 %}
                    <a>{{ recent.Title }}</a>
                  {% endfor %}
                  </aside>
                  """
                : "<main>{{ page.ContentPreRendered }}</main>");

        var today = DateTime.UtcNow.Date;
        for (var post = 1; post <= scenario.PostCount; post++)
        {
            var title = $"Post {post:D5}";
            var date = today.AddDays(-post).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var postBody = $"""
                ---
                title: "{title}"
                date: {date}
                tags:
                  - performance
                  - static-sites
                  - benchmark
                ---
                {title} exercises Markdown parsing, taxonomy generation, and page rendering.
                """;
            File.WriteAllText(Path.Combine(contentDirectory, $"post-{post:D5}.md"), postBody);
        }
    }

    BenchmarkBaseline WriteBenchmarkBaseline(IReadOnlyList<BenchmarkMeasurement> measurements)
    {
        var baseline = new BenchmarkBaseline(RuntimeIdentifier, RuntimeInformation.ProcessArchitecture.ToString(),
            BenchmarkProcessorCount, BenchmarkRunsPerScenario, DateTimeOffset.UtcNow,
            measurements.Select(measurement => new BenchmarkBaselineEntry(measurement.Name,
                measurement.MedianMilliseconds)).ToArray());

        BenchmarkBaselineFile.WriteAllText(JsonSerializer.Serialize(baseline, BenchmarkJsonOptions));
        Log.Information("Updated benchmark baseline at {Path}", BenchmarkBaselineFile);
        return baseline;
    }

    BenchmarkBaseline ReadBenchmarkBaseline()
    {
        if (!BenchmarkBaselineFile.FileExists())
        {
            throw new FileNotFoundException(
                $"Benchmark baseline is missing. Create it with './build.sh Benchmark --update-benchmark-baseline'.",
                BenchmarkBaselineFile);
        }

        return JsonSerializer.Deserialize<BenchmarkBaseline>(
                   BenchmarkBaselineFile.ReadAllText(), BenchmarkJsonOptions)
               ?? throw new InvalidDataException($"Could not deserialize benchmark baseline '{BenchmarkBaselineFile}'.");
    }

    void ValidateBenchmarkBaseline(BenchmarkBaseline baseline)
    {
        if (baseline.RunsPerScenario != BenchmarkRunsPerScenario)
        {
            throw new InvalidDataException(
                $"Benchmark baseline uses {baseline.RunsPerScenario} runs per scenario; " +
                $"expected {BenchmarkRunsPerScenario}.");
        }

        if (baseline.RuntimeIdentifier != RuntimeIdentifier ||
            baseline.Architecture != RuntimeInformation.ProcessArchitecture.ToString() ||
            baseline.ProcessorCount != BenchmarkProcessorCount)
        {
            throw new InvalidDataException(
                $"Benchmark baseline targets {baseline.RuntimeIdentifier}/{baseline.Architecture}/" +
                $"{baseline.ProcessorCount} processors, but this run targets {RuntimeIdentifier}/" +
                $"{RuntimeInformation.ProcessArchitecture}/{BenchmarkProcessorCount} processors. " +
                "Refresh the baseline on the benchmark runner.");
        }
    }

    sealed record BenchmarkScenario(int PostCount, bool HasSidebar)
    {
        public string Name => $"{PostCount}-posts-sidebar-{(HasSidebar ? "on" : "off")}";
    }

    sealed record BenchmarkMeasurement(
        string Name,
        int PostCount,
        bool HasSidebar,
        IReadOnlyList<double> RunTimesMilliseconds,
        double MedianMilliseconds);

    sealed record BenchmarkBaseline(
        string RuntimeIdentifier,
        string Architecture,
        int ProcessorCount,
        int RunsPerScenario,
        DateTimeOffset UpdatedAtUtc,
        IReadOnlyList<BenchmarkBaselineEntry> Scenarios);

    sealed record BenchmarkBaselineEntry(string Name, double MedianMilliseconds);

    sealed record BenchmarkReport(
        DateTimeOffset GeneratedAtUtc,
        string RuntimeIdentifier,
        string Architecture,
        string FrameworkVersion,
        int ProcessorCount,
        bool Passed,
        IReadOnlyList<BenchmarkScenarioReport> Scenarios);

    sealed record BenchmarkScenarioReport(
        string Name,
        int PostCount,
        bool HasSidebar,
        IReadOnlyList<double> RunTimesMilliseconds,
        double MedianMilliseconds,
        double BaselineMedianMilliseconds,
        double RegressionPercent,
        bool Passed);
}
