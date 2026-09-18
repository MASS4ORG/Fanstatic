using System.Net;
using Serilog;
using Fanstatic.Helpers;
using Fanstatic.Models;
using Fanstatic.ServerHandlers;

namespace Fanstatic.Commands.Serve;

/// <summary>
/// Serve Command will live serve the site and watch any changes.
/// </summary>
public sealed class ServeCommand : BaseGeneratorCommand, IDisposable
{
    /// <summary>
    /// Default values for the ServeCommand
    /// </summary>
    public const string BaseUrlDefault = "http://localhost";

    /// <summary>
    /// Default values for the ServeCommand
    /// </summary>
    public const int PortDefault = 2341;

    /// <summary>
    /// Default values for the ServeCommand
    /// </summary>
    public const int MaxPortTries = 10;

    /// <summary>
    /// The actual port being used after potential port selection
    /// </summary>
    public int PortUsed { get; private set; }

    /// <summary>
    /// The ServeOptions object containing the configuration parameters for the server.
    /// This includes settings such as the source directory to watch for file changes,
    /// verbosity of the logs, etc. These options are passed into the ServeCommand at construction
    /// and are used throughout its operation.
    /// </summary>
    readonly ServeOptions _options;

    /// <summary>
    /// The FileSystemWatcher object that monitors the source directory for file changes.
    /// When a change is detected, this triggers a server restart to ensure the served content
    /// remains up-to-date. The FileSystemWatcher is configured with the source directory
    /// at construction and starts watching immediately.
    /// </summary>
    readonly IFileWatcher _fileWatcher;

    readonly IPortSelector _portSelector;

    HttpListener? _listener;

    IServerHandlers[]? _handlers;

    DateTime _serverChangeTime;

    Task? _loop;

    readonly ILogger _logger;

    (WatcherChangeTypes changeType, string fullPath, DateTime dateTime) _lastFileChanged;

    // Add a CancellationTokenSource to manage server loop cancellation
    readonly CancellationTokenSource _cancellationTokenSource;

    readonly SemaphoreSlim _rebuildSemaphore = new(1, 1);

    bool _restartPending;

    readonly TimeSpan _debounceTime = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Constructor for the ServeCommand class.
    /// </summary>
    /// <param name="options">ServeOptions object specifying the serve options.</param>
    /// <param name="logger">The logger instance. Injectable for testing</param>
    /// <param name="fileWatcher"></param>
    /// <param name="fs"></param>
    /// <param name="portSelector"></param>
    public ServeCommand(
        ServeOptions options,
        ILogger logger,
        IFileWatcher fileWatcher,
        IFileSystem fs,
        IPortSelector portSelector)
        : base(options, logger, fs)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _fileWatcher = fileWatcher ?? throw new ArgumentNullException(nameof(fileWatcher));
        _portSelector = portSelector ?? throw new ArgumentNullException(nameof(portSelector));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cancellationTokenSource = new CancellationTokenSource();

        var sourceAbsolutePath = Path.GetFullPath(options.Source);
        logger.Information("Watching for file changes in {SourceAbsolutePath}", sourceAbsolutePath);
        fileWatcher.Start(sourceAbsolutePath, OnSourceFileChanged);

        Site.Fanstatic.IsServer = true;
    }

    /// <summary>
    /// Creates and executes a new ServeCommand instance
    /// </summary>
    /// <param name="options">The serve options</param>
    /// <param name="logger">The logger instance</param>
    /// <returns>A task with the result code (0 for success, 1 for failure)</returns>
    public static async Task<int> Create(ServeOptions options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        // Create a cancellation token source
        using var cancellationTokenSource = new CancellationTokenSource();

        try
        {
            using var sourceFileWatcher = new SourceFileWatcher();
            using var serveCommand = new ServeCommand(
                options,
                logger,
                sourceFileWatcher,
                new FileSystem(),
                new DefaultPortSelector(logger));

            // Handle Console Cancel event
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true; // Prevent the process from terminating immediately
                cancellationTokenSource.Cancel();
            };

            serveCommand.StartServer();

            try
            {
                // Wait until cancellation is requested
                await Task.Delay(Timeout.Infinite, cancellationTokenSource.Token);
            }
            catch (TaskCanceledException)
            {
                // Expected when Ctrl+C is pressed
            }
        }
        catch (Exception ex)
        {
            if (options.Verbose)
            {
                logger.Error(ex, "Serving failed");
            }
            else
            {
                logger.Error($"Serving failed: {ex.Message}");
            }

            return 1;
        }

        logger.Information("Fanstatic is over. Goodbye!");
        return 0;
    }

    /// <summary>
    /// Starts the server with intelligent port selection
    /// </summary>
    /// <param name="baseUrl">The base URL for the server.</param>
    /// <param name="requestedPort">The port number for the server.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public void StartServer(string baseUrl = "", int requestedPort = 0)
    {
        baseUrl = string.IsNullOrEmpty(baseUrl) ? BaseUrlDefault : baseUrl;
        requestedPort = requestedPort == 0 ? PortDefault : requestedPort;

        _logger.Information("Starting server...");
        Stopwatch.LogReport(Site.Title);
        _serverChangeTime = DateTime.UtcNow;

        PortUsed = _portSelector.SelectAvailablePort(baseUrl, requestedPort, MaxPortTries);
        var fullBaseUrl = new Uri($"{baseUrl}:{PortUsed}/");

        InitializeHandlers();

        _listener = new HttpListener();
        _listener.Prefixes.Add(fullBaseUrl.AbsoluteUri);
        _listener.Start();

        (Site as ISiteSettings).BaseUrl = fullBaseUrl;

        _logger.Information("Site is live: {fullBaseUrl}", fullBaseUrl);
        _logger.Information("Press Ctrl+C to stop");

        _loop = Task.Run(async () =>
        {
            try
            {
                while (!_cancellationTokenSource.Token.IsCancellationRequested
                       && _listener is not null
                       && _listener.IsListening)
                {
                    var context = await _listener.GetContextAsync()
                        .WaitAsync(_cancellationTokenSource.Token)
                        .ConfigureAwait(false);
                    await HandleRequest(context).ConfigureAwait(false);
                }
            }
            catch (HttpListenerException) when (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                _logger.Error("Unexpected listener error.");
            }
            catch (Exception ex) when (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                _logger.Error(ex, "Error processing request.");
            }
        }, _cancellationTokenSource.Token);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Cancel the server loop
        _cancellationTokenSource.Cancel();

        // Stop and close the listener
        _listener?.Stop();
        _listener?.Close();

        // Stop file watcher
        _fileWatcher.Stop();

        try
        {
            // Wait for the loop to complete
            _loop?.Wait(TimeSpan.FromSeconds(5));
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is TaskCanceledException))
        {
            _logger.Debug("AggregateException, but it's fine.");
        }
    }

    /// <summary>
    /// Restarts the server. In fact, only recreate the site and update the handlers.
    /// </summary>
    void RestartServer(CancellationToken cancellationToken)
    {
        try
        {
            _logger.Information("Recreating site...");

            // Check for cancellation before starting
            cancellationToken.ThrowIfCancellationRequested();

            // Reinitialize the site
            Site = SiteHelper.Init(ConfigFile, _options, Parser, _logger, Stopwatch, Fs);

            // Check for cancellation after site init
            cancellationToken.ThrowIfCancellationRequested();

            InitializeHandlers();
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("Site restart was canceled");
            throw;
        }
    }

    void InitializeHandlers()
    {
        _handlers =
        [
            new PingRequests(),
            new StaticFileRequest(Site.SourceStaticPath, false),
            new StaticFileRequest(Site.Theme?.StaticFolder, true),
            new RegisteredPageRequest(Site),
            new RegisteredPageResourceRequest(Site)
        ];

        _logger.Information("Site created.");
    }

    /// <summary>
    /// Handles the HTTP request asynchronously.
    /// </summary>
    /// <param name="context">The HttpContext representing the current request.</param>
    async Task HandleRequest(HttpListenerContext context)
    {
        var requestPath = context.Request.Url ?? new("");
        requestPath = new Uri(requestPath.AbsolutePath, UriKind.RelativeOrAbsolute);

        var resultType = await TryHandleRequestWithHandlers(context, requestPath);

        if (resultType is null)
        {
            resultType = "404";
            await HandleNotFoundRequest(context).ConfigureAwait(false);
        }
        else
        {
            context.Response.OutputStream.Close();
        }

        LogRequestResult(resultType, requestPath);
    }

    async Task<string?> TryHandleRequestWithHandlers(HttpListenerContext context, Uri requestPath)
    {
        if (_handlers is null)
        {
            return null;
        }

        var response = new HttpListenerResponseWrapper(context.Response);
        foreach (var handler in _handlers)
        {
            if (!handler.Check(requestPath))
            {
                continue;
            }

            try
            {
                return await handler.Handle(response, requestPath, _serverChangeTime).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.Debug(ex, "Error handling the request.");
            }
        }

        return null;
    }

    void LogRequestResult(string resultType, Uri requestPath)
    {
        _logger.Debug("Request {type}\tfor {RequestPath}", resultType, requestPath);
    }

    static async Task HandleNotFoundRequest(HttpListenerContext context)
    {
        context.Response.StatusCode = 404;
        await using var writer = new StreamWriter(context.Response.OutputStream);
        await writer.WriteAsync("404 - File Not Found 22").ConfigureAwait(false);
    }

    /// <summary>
    /// Handles the file change event from the file watcher.
    /// </summary>
    /// <param name="sender">The object that triggered the event.</param>
    /// <param name="e">The FileSystemEventArgs containing information about the file change.</param>
    async void OnSourceFileChanged(object sender, FileSystemEventArgs e)
    {
        if (e.FullPath.Contains(@".git", StringComparison.InvariantCulture))
        {
            return;
        }

        var now = DateTime.UtcNow;

        // Debounce: ignore changes too close together
        if ((now - _lastFileChanged.dateTime) < _debounceTime)
        {
            return;
        }

        _lastFileChanged = (e.ChangeType, e.FullPath, now);

        if (_rebuildSemaphore.CurrentCount == 0)
        {
            _logger.Debug("Restart already in progress. Marking pending restart.");
            _restartPending = true;
            return;
        }

        await _rebuildSemaphore.WaitAsync(_cancellationTokenSource.Token);
        try
        {
            do
            {
                _restartPending = false;
                _serverChangeTime = DateTime.UtcNow;

                RestartServer(_cancellationTokenSource.Token);

                // If another change was queued during restart, loop to handle it
            } while (_restartPending);
        }
        catch (OperationCanceledException)
        {
            _logger.Debug("Site restart was canceled");
        }
        finally
        {
            _rebuildSemaphore.Release();
        }
    }
}
