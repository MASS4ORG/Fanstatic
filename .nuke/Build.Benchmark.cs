using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace Fanstatic.NUKE;

sealed partial class Build
{
    const int BenchmarkRunsPerScenario = 5;
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

    [UsedImplicitly]
    Target Benchmark => td => td
        .DependsOn(Restore)
        .Produces(BenchmarkReportFile)
        .Executes(RunBenchmark);

    void RunBenchmark()
    {
        var sdkVersion = ReadDotNetSdkVersion();
        if (!System.Version.TryParse(sdkVersion, out var parsedSdkVersion) || parsedSdkVersion.Major != 10)
        {
            throw new InvalidOperationException(
                $"Benchmarks must use the .NET 10 SDK; the selected SDK is '{sdkVersion}'.");
        }

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
            ? WriteBenchmarkBaseline(measurements, sdkVersion)
            : ReadBenchmarkBaseline();
        ValidateBenchmarkBaseline(baseline, sdkVersion);

        var reportScenarios = measurements.Select(measurement =>
        {
            var baselineMedian = baseline.Scenarios
                .SingleOrDefault(entry => entry.Name == measurement.Name)
                ?? throw new InvalidDataException($"Benchmark baseline is missing scenario '{measurement.Name}'.");
            if (baselineMedian.ParseMedianMilliseconds <= 0 || baselineMedian.CreateMedianMilliseconds <= 0)
            {
                throw new InvalidDataException($"Benchmark baseline phases for '{measurement.Name}' must be positive.");
            }

            var parseRegressionPercent =
                (measurement.ParseMedianMilliseconds - baselineMedian.ParseMedianMilliseconds)
                / baselineMedian.ParseMedianMilliseconds * 100;
            var createRegressionPercent =
                (measurement.CreateMedianMilliseconds - baselineMedian.CreateMedianMilliseconds)
                / baselineMedian.CreateMedianMilliseconds * 100;
            var passed = measurement.ParseMedianMilliseconds <=
                         baselineMedian.ParseMedianMilliseconds * (1 + MaximumBenchmarkRegressionPercent / 100d)
                         && measurement.CreateMedianMilliseconds <=
                         baselineMedian.CreateMedianMilliseconds * (1 + MaximumBenchmarkRegressionPercent / 100d);

            Log.Information(
                "Benchmark {Scenario}: Parse {Parse:F1}/{BaselineParse:F1} ms ({ParseRegression:F1}%), " +
                "Create {Create:F1}/{BaselineCreate:F1} ms ({CreateRegression:F1}%), {Status}",
                measurement.Name, measurement.ParseMedianMilliseconds, baselineMedian.ParseMedianMilliseconds,
                parseRegressionPercent, measurement.CreateMedianMilliseconds,
                baselineMedian.CreateMedianMilliseconds, createRegressionPercent,
                passed ? "PASS" : "FAIL");

            return new BenchmarkScenarioReport(measurement.Name, measurement.PostCount, measurement.HasSidebar,
                measurement.Runs, measurement.ParseMedianMilliseconds, measurement.CreateMedianMilliseconds,
                measurement.WallMedianMilliseconds, baselineMedian.ParseMedianMilliseconds,
                baselineMedian.CreateMedianMilliseconds, parseRegressionPercent, createRegressionPercent, passed);
        }).ToArray();

        var report = new BenchmarkReport(DateTimeOffset.UtcNow, RuntimeIdentifier,
            RuntimeInformation.ProcessArchitecture.ToString(), sdkVersion, Environment.Version.ToString(),
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

    static string ReadDotNetSdkVersion()
    {
        var startInfo = new ProcessStartInfo("dotnet", "--version")
        {
            WorkingDirectory = RootDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not determine the selected .NET SDK version.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Could not determine the selected .NET SDK version.{Environment.NewLine}{standardError}");
        }

        return standardOutput.Trim();
    }

    static double Median(IEnumerable<double> samples) =>
        samples.Order().ElementAt(BenchmarkRunsPerScenario / 2);

    static double ReadPhaseDuration(string buildLog, string phase)
    {
        var match = Regex.Match(buildLog,
            $@"(?m)^\s*{Regex.Escape(phase)}\s+\d+\s+(?<duration>\d+)\s+ms\s*$");
        if (!match.Success ||
            !double.TryParse(match.Groups["duration"].Value, CultureInfo.InvariantCulture, out var duration))
        {
            throw new InvalidDataException($"Could not read the '{phase}' phase duration from the build report.");
        }

        return duration;
    }

    BenchmarkMeasurement RunBenchmarkScenario(string executable, string tempDirectory, BenchmarkScenario scenario)
    {
        var siteDirectory = Path.Combine(tempDirectory, scenario.Name);
        CreateBenchmarkSite(siteDirectory, scenario);

        List<BenchmarkRun> runs = [];
        for (var run = 1; run <= BenchmarkRunsPerScenario; run++)
        {
            var outputDirectory = Path.Combine(tempDirectory, "output", $"{scenario.Name}-{run}");
            runs.Add(RunBenchmarkBuild(executable, siteDirectory, outputDirectory));
            if (Directory.Exists(outputDirectory))
            {
                Directory.Delete(outputDirectory, recursive: true);
            }
        }

        return new BenchmarkMeasurement(scenario.Name, scenario.PostCount, scenario.HasSidebar, runs,
            Median(runs.Select(run => run.ParseMilliseconds)),
            Median(runs.Select(run => run.CreateMilliseconds)),
            Median(runs.Select(run => run.WallMilliseconds)));
    }

    static BenchmarkRun RunBenchmarkBuild(string executable, string siteDirectory, string outputDirectory)
    {
        var startInfo = new ProcessStartInfo(executable)
        {
            WorkingDirectory = siteDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            Environment =
            {
                ["DOTNET_PROCESSOR_COUNT"] = BenchmarkProcessorCount.ToString(CultureInfo.InvariantCulture)
            }
        };
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

        var buildLog = standardOutput + Environment.NewLine + standardError;
        return new BenchmarkRun(
            ReadPhaseDuration(buildLog, "Parse"),
            ReadPhaseDuration(buildLog, "Create"),
            stopwatch.Elapsed.TotalMilliseconds);
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

        File.WriteAllText(Path.Combine(themeDirectory, "single.html"), """
            <article>{{ page.ContentPreRendered }}</article>
            <span>{{ page.WordCount }}</span>
            <span>{{ page.Taxonomies.tags | size }}</span>
            <span>{{ site.Taxonomies.tags | size }}</span>
            """);
        var listTemplate = """
            <main>{{ page.ContentPreRendered }}</main>
            {% for child in site.RegularPages %}
              {{ child.Content }}
              {{ child.WordCount }}
              {{ child.Taxonomies.tags | size }}
            {% endfor %}
            <span>{{ site.Taxonomies.tags | size }}</span>
            """;
        if (scenario.HasSidebar)
        {
            listTemplate += """
                <aside>
                {% for recent in site.RegularPagesByDate reversed limit: 10 %}
                  <a>{{ recent.Title }}</a>
                {% endfor %}
                </aside>
                """;
        }

        File.WriteAllText(Path.Combine(themeDirectory, "list.html"), listTemplate);

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

    BenchmarkBaseline WriteBenchmarkBaseline(
        IReadOnlyList<BenchmarkMeasurement> measurements,
        string sdkVersion)
    {
        var baseline = new BenchmarkBaseline(sdkVersion, RuntimeIdentifier,
            RuntimeInformation.ProcessArchitecture.ToString(), Environment.Version.ToString(),
            BenchmarkProcessorCount, BenchmarkRunsPerScenario, DateTimeOffset.UtcNow,
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" ? "GitHub Actions" : "local",
            Environment.MachineName,
            measurements.Select(measurement => new BenchmarkBaselineEntry(measurement.Name,
                measurement.ParseMedianMilliseconds, measurement.CreateMedianMilliseconds)).ToArray());

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

    void ValidateBenchmarkBaseline(BenchmarkBaseline baseline, string sdkVersion)
    {
        if (baseline.RunsPerScenario != BenchmarkRunsPerScenario)
        {
            throw new InvalidDataException(
                $"Benchmark baseline uses {baseline.RunsPerScenario} runs per scenario; " +
                $"expected {BenchmarkRunsPerScenario}.");
        }

        if (!StringComparer.Ordinal.Equals(baseline.SdkVersion, sdkVersion) ||
            !StringComparer.Ordinal.Equals(baseline.RuntimeVersion, Environment.Version.ToString()))
        {
            throw new InvalidDataException(
                $"Benchmark baseline was measured with SDK/runtime {baseline.SdkVersion}/{baseline.RuntimeVersion}; " +
                $"this run uses {sdkVersion}/{Environment.Version}. Refresh the baseline on the benchmark runner.");
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
        IReadOnlyList<BenchmarkRun> Runs,
        double ParseMedianMilliseconds,
        double CreateMedianMilliseconds,
        double WallMedianMilliseconds);

    sealed record BenchmarkRun(double ParseMilliseconds, double CreateMilliseconds, double WallMilliseconds);

    sealed record BenchmarkBaseline(
        string SdkVersion,
        string RuntimeIdentifier,
        string Architecture,
        string RuntimeVersion,
        int ProcessorCount,
        int RunsPerScenario,
        DateTimeOffset UpdatedAtUtc,
        string Source,
        string Runner,
        IReadOnlyList<BenchmarkBaselineEntry> Scenarios);

    sealed record BenchmarkBaselineEntry(
        string Name, double ParseMedianMilliseconds, double CreateMedianMilliseconds);

    [UsedImplicitly(ImplicitUseTargetFlags.Members)]
    sealed record BenchmarkReport(
        DateTimeOffset GeneratedAtUtc,
        string RuntimeIdentifier,
        string Architecture,
        string SdkVersion,
        string RuntimeVersion,
        int ProcessorCount,
        bool Passed,
        IReadOnlyList<BenchmarkScenarioReport> Scenarios);

    [UsedImplicitly(ImplicitUseTargetFlags.Members)]
    sealed record BenchmarkScenarioReport(
        string Name,
        int PostCount,
        bool HasSidebar,
        IReadOnlyList<BenchmarkRun> Runs,
        double ParseMedianMilliseconds,
        double CreateMedianMilliseconds,
        double WallMedianMilliseconds,
        double BaselineParseMedianMilliseconds,
        double BaselineCreateMedianMilliseconds,
        double ParseRegressionPercent,
        double CreateRegressionPercent,
        bool Passed);
}
