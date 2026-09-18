using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using CommandLine;
using Serilog;
using Serilog.Events;
using Fanstatic.Commands;
using Fanstatic.Commands.API;
using Fanstatic.Commands.Build;
using Fanstatic.Commands.NewSite;
using Fanstatic.Commands.NewTheme;
using Fanstatic.Commands.Serve;
using Fanstatic.Commands.ValidateLinks;
using Fanstatic.Helpers;

namespace Fanstatic;

/// <summary>
/// The main entry point of the program.
/// </summary>
/// <remarks>
/// Constructor
/// </remarks>
public class Program(ILogger loggerInitial)
{
    ILogger _logger = null!;

    /// <summary>
    /// Basic logo of the program, for fun
    /// </summary>
    static readonly string HelloWorld = @"
░█▀▀░█▀█░█▀█░█▀▀░▀█▀░█▀█░▀█▀░█░█▀▀
░█▀▀░█▀█░█░█░▀▀█░░█░░█▀█░░█░░█░█░░
░▀░░░▀░▀░▀░▀░▀▀▀░░▀░░▀░▀░░▀░░▀░▀▀▀";

    /// <summary>
    /// Entry point of the program
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    public static async Task<int> Main(string[] args)
    {
        var program = new Program(CreateLogger());
        return await program.RunCommandLine(args).ConfigureAwait(false);
    }

    /// <summary>
    /// Actual entrypoint of the program
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(GenerateOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(BuildOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ServeOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(NewSiteOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(NewThemeOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ValidateLinksOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(ApiGeneratorOptions))]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, "CommandLine.VerbAttribute", "CommandLineParser")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, "CommandLine.OptionAttribute", "CommandLineParser")]
    async Task<int> RunCommandLine(string[] args)
    {
        OutputLogo();
        OutputWelcome();
        _logger = loggerInitial;
        return await Parser.Default.ParseArguments<
                BuildOptions,
                ServeOptions,
                NewSiteOptions,
                NewThemeOptions,
                ApiGeneratorOptions,
                ValidateLinksOptions>(args)
            .WithParsed<GenerateOptions>(options => _logger = CreateLogger(options.Verbose))
            .WithParsed<BuildOptions>(options => options.Output = string.IsNullOrEmpty(options.Output)
                    ? Path.Combine(options.Source, "public")
                    : options.Output)
            .MapResult(
                (BuildOptions options) => BuildCommand.Create(options, _logger),
                async (ServeOptions options) => await ServeCommand.Create(options, _logger),
                (NewSiteOptions options) => NewSiteCommand.Create(options, _logger),
                (NewThemeOptions options) => NewThemeCommand.Create(options, _logger),
                (ApiGeneratorOptions options) => ApiGeneratorCommand.Create(options, _logger, new FileSystem()),
                (ValidateLinksOptions options) => new ValidateLinksCommand(options, _logger, new FileSystem())
                    .Parse()
                    .ContinueWith(task => task.Result.GenerateReport())
                    .Unwrap(),
                _ => Task.FromResult(0)
            ).ConfigureAwait(false);
    }

    /// <summary>
    /// Create a log (normally from Serilog), depending on the verbose option
    /// </summary>
    /// <param name="verbose"></param>
    /// <returns></returns>
    public static ILogger CreateLogger(bool verbose = false) => new LoggerConfiguration()
        .MinimumLevel.Is(verbose ? LogEventLevel.Debug : LogEventLevel.Information)
        // .WriteTo.Async(a => a.Console(formatProvider: System.Globalization.CultureInfo.CurrentCulture))
        .WriteTo.Console(formatProvider: CultureInfo.CurrentCulture)
        .CreateLogger();

    /// <summary>
    /// Print the name and version of the program.
    /// </summary>
    public void OutputWelcome()
    {
        var assemblyName = Assembly.GetExecutingAssembly().GetName();
        var appName = assemblyName.Name;
        var appVersion = assemblyName.Version;
        loggerInitial.Information("{name} v{version}", appName, appVersion);
    }

    /// <summary>
    /// Print the logo
    /// </summary>
    public void OutputLogo()
    {
        loggerInitial.Information(HelloWorld);
    }
}
