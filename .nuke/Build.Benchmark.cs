using System.Runtime.InteropServices;
using JetBrains.Annotations;

namespace Fanstatic.NUKE;

sealed partial class Build
{
    const int BenchmarkRunsPerScenario = 5;
    const int MaximumBenchmarkRegressionPercent = 25;
    const int BenchmarkProcessorCount = 2;
    const string CiBenchmarkSource = "GitHub Actions";
    const string LocalBenchmarkSource = "local";

    static readonly int[] BenchmarkPostCounts = [1000, 2000, 4000];

    static readonly JsonSerializerOptions BenchmarkJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    [Parameter("Append the measured results to the benchmark history as the entry for the project version")]
    readonly bool RecordBenchmark;

    AbsolutePath BenchmarkHistoryFile => RootDirectory / ".nuke" / "benchmark-history.json";
    AbsolutePath BenchmarkReportFile => RootDirectory / "artifacts" / "benchmark.json";
    AbsolutePath BenchmarkPublishDirectory => RootDirectory / ".publish" / "benchmark" / RuntimeIdentifier;

    [UsedImplicitly]
    public Target Benchmark => td => td
        .DependsOn(Restore)
        .Produces(BenchmarkReportFile)
        .Executes(() => RunBenchmark(RecordBenchmark, failOnRegression: true));

    /// <summary>
    /// Records the benchmark entry for the version being released, so the release commit carries it. A regression
    /// is logged but does not stop the release: the history must show what was shipped.
    /// </summary>
    [UsedImplicitly]
    public Target RecordReleaseBenchmark => td => td
        .DependsOn(CheckNewCommits, Restore)
        .OnlyWhenDynamic(() => HasNewCommits)
        .Executes(() => RunBenchmark(record: true, failOnRegression: false));

    void RunBenchmark(bool record, bool failOnRegression)
    {
        var sdkVersion = ReadDotNetSdkVersion();
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

        var current = CreateBenchmarkHistoryEntry(measurements, sdkVersion);
        var history = ReadBenchmarkHistory();
        var reference = FindBenchmarkReference(history, current);
        LogBenchmarkReference(reference, current);

        var reportScenarios = measurements.Select(measurement => CompareBenchmarkScenario(measurement, reference))
            .ToArray();

        var report = new BenchmarkReport(DateTimeOffset.UtcNow, RuntimeIdentifier,
            RuntimeInformation.ProcessArchitecture.ToString(), sdkVersion, Environment.Version.ToString(),
            BenchmarkProcessorCount, reference?.Version, reportScenarios.All(scenario => scenario.Passed),
            reportScenarios);

        _ = BenchmarkReportFile.Parent.CreateDirectory();
        BenchmarkReportFile.WriteAllText(JsonSerializer.Serialize(report, BenchmarkJsonOptions));

        if (!report.Passed)
        {
            var message =
                $"Benchmark regression exceeded {MaximumBenchmarkRegressionPercent}%; see '{BenchmarkReportFile}'.";
            if (failOnRegression)
            {
                throw new InvalidOperationException(message);
            }

            Log.Warning(message);
        }

        if (record)
        {
            AppendBenchmarkHistoryEntry(history, current);
        }
    }

    BenchmarkScenarioReport CompareBenchmarkScenario(
        BenchmarkMeasurement measurement,
        BenchmarkHistoryEntry reference)
    {
        var referenceScenario = reference?.Scenarios.SingleOrDefault(entry => entry.Name == measurement.Name);
        if (referenceScenario is null)
        {
            Log.Information("Benchmark {Scenario}: Parse {Parse:F1} ms, Create {Create:F1} ms, no reference",
                measurement.Name, measurement.ParseMedianMilliseconds, measurement.CreateMedianMilliseconds);
            return new BenchmarkScenarioReport(measurement.Name, measurement.PostCount, measurement.HasSidebar,
                measurement.Runs, measurement.ParseMedianMilliseconds, measurement.CreateMedianMilliseconds,
                measurement.WallMedianMilliseconds, null, null, null, null, true);
        }

        if (referenceScenario.ParseMedianMilliseconds <= 0 || referenceScenario.CreateMedianMilliseconds <= 0)
        {
            throw new InvalidDataException(
                $"Benchmark reference phases for '{measurement.Name}' must be positive.");
        }

        var parseRegressionPercent =
            (measurement.ParseMedianMilliseconds - referenceScenario.ParseMedianMilliseconds)
            / referenceScenario.ParseMedianMilliseconds * 100;
        var createRegressionPercent =
            (measurement.CreateMedianMilliseconds - referenceScenario.CreateMedianMilliseconds)
            / referenceScenario.CreateMedianMilliseconds * 100;
        var passed = parseRegressionPercent <= MaximumBenchmarkRegressionPercent
                     && createRegressionPercent <= MaximumBenchmarkRegressionPercent;

        Log.Information(
            "Benchmark {Scenario}: Parse {Parse:F1}/{ReferenceParse:F1} ms ({ParseRegression:F1}%), " +
            "Create {Create:F1}/{ReferenceCreate:F1} ms ({CreateRegression:F1}%), {Status}",
            measurement.Name, measurement.ParseMedianMilliseconds, referenceScenario.ParseMedianMilliseconds,
            parseRegressionPercent, measurement.CreateMedianMilliseconds,
            referenceScenario.CreateMedianMilliseconds, createRegressionPercent,
            passed ? "PASS" : "FAIL");

        return new BenchmarkScenarioReport(measurement.Name, measurement.PostCount, measurement.HasSidebar,
            measurement.Runs, measurement.ParseMedianMilliseconds, measurement.CreateMedianMilliseconds,
            measurement.WallMedianMilliseconds, referenceScenario.ParseMedianMilliseconds,
            referenceScenario.CreateMedianMilliseconds, parseRegressionPercent, createRegressionPercent, passed);
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

    BenchmarkHistoryEntry CreateBenchmarkHistoryEntry(
        IReadOnlyList<BenchmarkMeasurement> measurements,
        string sdkVersion) =>
        new(Version, DateTimeOffset.UtcNow,
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" ? CiBenchmarkSource : LocalBenchmarkSource,
            Environment.MachineName, sdkVersion, Environment.Version.ToString(), RuntimeIdentifier,
            RuntimeInformation.ProcessArchitecture.ToString(), BenchmarkProcessorCount, BenchmarkRunsPerScenario,
            measurements.Select(measurement => new BenchmarkHistoryScenario(measurement.Name,
                measurement.ParseMedianMilliseconds, measurement.CreateMedianMilliseconds)).ToArray());

    BenchmarkHistory ReadBenchmarkHistory()
    {
        if (!BenchmarkHistoryFile.FileExists())
        {
            return new BenchmarkHistory([]);
        }

        return JsonSerializer.Deserialize<BenchmarkHistory>(
                   BenchmarkHistoryFile.ReadAllText(), BenchmarkJsonOptions)
               ?? throw new InvalidDataException($"Could not deserialize benchmark history '{BenchmarkHistoryFile}'.");
    }

    void AppendBenchmarkHistoryEntry(BenchmarkHistory history, BenchmarkHistoryEntry entry)
    {
        if (history.Entries.Any(existing => IsSameBenchmarkSeries(existing, entry) && existing.Version == entry.Version))
        {
            throw new InvalidOperationException(
                $"The benchmark history already has an entry for version {entry.Version} from this runner. " +
                "Remove it from the history file first to record it again.");
        }

        var updated = new BenchmarkHistory([.. history.Entries, entry]);
        BenchmarkHistoryFile.WriteAllText(JsonSerializer.Serialize(updated, BenchmarkJsonOptions));
        Log.Information("Recorded benchmark entry for version {Version} in {Path}", entry.Version,
            BenchmarkHistoryFile);
    }

    /// <summary>
    /// The newest history entry measured under the same conditions, so the gate never compares a CI runner with a
    /// developer machine or different parallelism. A different SDK or runtime does not prevent the comparison.
    /// </summary>
    static BenchmarkHistoryEntry FindBenchmarkReference(BenchmarkHistory history, BenchmarkHistoryEntry current) =>
        history.Entries
            .Where(entry => IsSameBenchmarkSeries(entry, current))
            .OrderByDescending(entry => entry.RecordedAtUtc)
            .FirstOrDefault();

    static bool IsSameBenchmarkSeries(BenchmarkHistoryEntry entry, BenchmarkHistoryEntry other) =>
        entry.Source == other.Source
        && (entry.Source != LocalBenchmarkSource || entry.Runner == other.Runner)
        && entry.RuntimeIdentifier == other.RuntimeIdentifier
        && entry.Architecture == other.Architecture
        && entry.ProcessorCount == other.ProcessorCount
        && entry.RunsPerScenario == other.RunsPerScenario;

    static void LogBenchmarkReference(BenchmarkHistoryEntry reference, BenchmarkHistoryEntry current)
    {
        if (reference is null)
        {
            Log.Warning(
                "The benchmark history has no comparable entry for {Source}/{Runner}; nothing is gated. " +
                "Record one with './build.sh Benchmark --record-benchmark'.", current.Source, current.Runner);
            return;
        }

        Log.Information("Comparing with the benchmark entry for version {Version} recorded {RecordedAt:u}",
            reference.Version, reference.RecordedAtUtc);
        if (!HaveSameMajorVersion(reference.SdkVersion, current.SdkVersion)
            || !HaveSameMajorVersion(reference.RuntimeVersion, current.RuntimeVersion))
        {
            Log.Warning("The reference was measured with SDK/runtime {Reference}, this run uses {Current}",
                $"{reference.SdkVersion}/{reference.RuntimeVersion}",
                $"{current.SdkVersion}/{current.RuntimeVersion}");
        }
    }

    /// <summary>
    /// Patch releases change with every runner image update, so only a major version change is worth a warning.
    /// </summary>
    static bool HaveSameMajorVersion(string left, string right) =>
        int.TryParse(left.Split('.')[0], out var leftMajor)
        && int.TryParse(right.Split('.')[0], out var rightMajor)
        && leftMajor == rightMajor;

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

    sealed record BenchmarkHistory(IReadOnlyList<BenchmarkHistoryEntry> Entries);

    sealed record BenchmarkHistoryEntry(
        string Version,
        DateTimeOffset RecordedAtUtc,
        string Source,
        string Runner,
        string SdkVersion,
        string RuntimeVersion,
        string RuntimeIdentifier,
        string Architecture,
        int ProcessorCount,
        int RunsPerScenario,
        IReadOnlyList<BenchmarkHistoryScenario> Scenarios);

    sealed record BenchmarkHistoryScenario(
        string Name, double ParseMedianMilliseconds, double CreateMedianMilliseconds);

    [UsedImplicitly(ImplicitUseTargetFlags.Members)]
    sealed record BenchmarkReport(
        DateTimeOffset GeneratedAtUtc,
        string RuntimeIdentifier,
        string Architecture,
        string SdkVersion,
        string RuntimeVersion,
        int ProcessorCount,
        string ReferenceVersion,
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
        double? ReferenceParseMedianMilliseconds,
        double? ReferenceCreateMedianMilliseconds,
        double? ParseRegressionPercent,
        double? CreateRegressionPercent,
        bool Passed);
}
