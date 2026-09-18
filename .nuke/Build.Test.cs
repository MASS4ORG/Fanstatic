namespace Build;

/// <summary>
/// This is the main build file for the project.
/// This partial is responsible for the build process.
/// </summary>
partial class Build
{
    AbsolutePath TestProjectDirectory => Solution.Fanstatic_Test.Directory;
    static AbsolutePath CoverageDirectory => RootDirectory / "coverage";
    static AbsolutePath CoverageResultFile => CoverageDirectory / "coverage.xml";
    static AbsolutePath CoverageReportDirectory => CoverageDirectory / "report";
    static AbsolutePath CoverageReportSummaryDirectory => CoverageReportDirectory / "Summary.txt";
    AbsolutePath CoverageSettingsFile => TestProjectDirectory / "CodeCoverage.runsettings";

    private Target Test => td => td
        .After(Compile)
        .Produces(CoverageResultFile)
        .Executes(() =>
            {
                _ = CoverageDirectory.CreateDirectory();
                DotNetTasks.DotNetRun(settings => settings
                    .SetConfiguration(Configuration)
                    .SetProjectFile(Solution.Fanstatic_Test.Path)
                    .SetApplicationArguments(
                        "--coverage",
                        "--coverage-settings", CoverageSettingsFile, // Excludes source generated files
                        "--coverage-output-format", "cobertura",
                        "--coverage-output", CoverageResultFile)
                );
            }
        );

    public Target TestReport => td => td
        .DependsOn(Test)
        .Consumes(Test, CoverageResultFile)
        .Executes(() =>
        {
            _ = CoverageReportDirectory.CreateDirectory();
            _ = ReportGeneratorTasks.ReportGenerator(s => s
                .SetTargetDirectory(CoverageReportDirectory)
                .SetReportTypes(ReportTypes.Html, ReportTypes.TextSummary)
                .SetReports(CoverageResultFile)
            );
            var summaryText = CoverageReportSummaryDirectory.ReadAllLines();
            Log.Information(string.Join(Environment.NewLine, summaryText));
        });
}
