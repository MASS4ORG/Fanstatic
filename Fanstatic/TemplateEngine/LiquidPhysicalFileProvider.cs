using System.Collections.Frozen;
using System.Text;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Primitives;

namespace Fanstatic.TemplateEngine;

/// <summary>
/// Custom IFileProvider that strips the ".liquid" extension Fluid appends,
/// serves files directly from disk, and falls back to built-in partials.
/// Does NOT extend PhysicalFileProvider to avoid creating internal FileSystemWatchers
/// that would trigger the SourceFileWatcher and cause infinite site recreation.
/// </summary>
public class LiquidPhysicalFileProvider : IFileProvider
{
    readonly string _root;

    /// <summary>
    /// ctr
    /// </summary>
    public LiquidPhysicalFileProvider(string root)
    {
        _root = Path.GetFullPath(root);
    }

    /// <summary>
    /// Remove the ".liquid" extension; serve from disk; fall back to built-in partials.
    /// </summary>
    public IFileInfo GetFileInfo(string subpath)
    {
        subpath = Path.ChangeExtension(subpath, null);
        var relativePath = subpath.TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(_root, relativePath);

        if (File.Exists(fullPath))
            return new PhysicalFileInfoWrapper(new FileInfo(fullPath));

        var key = subpath.TrimStart('/').Replace('\\', '/');
        if (BuiltinPartials.TryGetValue(key, out var content))
            return new StringFileInfo(Path.GetFileName(subpath), content);

        return new NotFoundFileInfo(subpath);
    }

    /// <inheritdoc/>
    public IDirectoryContents GetDirectoryContents(string subpath) => NotFoundDirectoryContents.Singleton;

    /// <summary>
    /// Returns a no-op token — no file watching so serve mode does not loop.
    /// </summary>
    public IChangeToken Watch(string filter) => NullChangeToken.Singleton;

    static readonly FrozenDictionary<string, string> BuiltinPartials =
        new Dictionary<string, string>
        {
            {
                "partials/pagination.html",
                """
                {% if paginator %}
                <nav aria-label="Pagination">
                  {% if paginator.Prev %}
                    {% if paginator.Prev == 1 %}
                      <a href="{{ paginator.BaseUrl }}">&laquo; Previous</a>
                    {% else %}
                      <a href="{{ paginator.BaseUrl }}{{ paginator.PaginatePath }}/{{ paginator.Prev }}/">&laquo; Previous</a>
                    {% endif %}
                  {% else %}
                    <span>&laquo; Previous</span>
                  {% endif %}

                  {% for pageNum in paginator.Pages %}
                    {% if pageNum == paginator.Current %}
                      <span>{{ pageNum }}</span>
                    {% elsif pageNum == 1 %}
                      <a href="{{ paginator.BaseUrl }}">{{ pageNum }}</a>
                    {% else %}
                      <a href="{{ paginator.BaseUrl }}{{ paginator.PaginatePath }}/{{ pageNum }}/">{{ pageNum }}</a>
                    {% endif %}
                  {% endfor %}

                  {% if paginator.Next %}
                    <a href="{{ paginator.BaseUrl }}{{ paginator.PaginatePath }}/{{ paginator.Next }}/">Next &raquo;</a>
                  {% else %}
                    <span>Next &raquo;</span>
                  {% endif %}
                </nav>
                {% endif %}
                """
            }
        }.ToFrozenDictionary();

    sealed class PhysicalFileInfoWrapper(FileInfo fileInfo) : IFileInfo
    {
        public bool Exists => fileInfo.Exists;
        public long Length => fileInfo.Length;
        public string? PhysicalPath => fileInfo.FullName;
        public string Name => fileInfo.Name;
        public DateTimeOffset LastModified => fileInfo.LastWriteTimeUtc;
        public bool IsDirectory => false;
        public Stream CreateReadStream() => fileInfo.OpenRead();
    }

    sealed class StringFileInfo(string name, string content) : IFileInfo
    {
        readonly byte[] _bytes = Encoding.UTF8.GetBytes(content);

        public bool Exists => true;
        public long Length => _bytes.Length;
        public string? PhysicalPath => null;
        public string Name => name;
        public DateTimeOffset LastModified => DateTimeOffset.UtcNow;
        public bool IsDirectory => false;
        public Stream CreateReadStream() => new MemoryStream(_bytes);
    }
}
