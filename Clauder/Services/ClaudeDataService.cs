using System.Text.Json;
using Clauder.Models;
using Concur;
using Concur.Implementations;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using static Concur.ConcurRoutine;

namespace Clauder.Services;

public class ClaudeDataService : IDisposable
{
    private readonly static string HomeDirectory = Environment.GetEnvironmentVariable("HOME")!;
    private readonly static string ClaudeDirectory = Path.Combine(HomeDirectory, ".claude");
    private readonly static string ProjectDir = Path.Combine(ClaudeDirectory, "projects");

    private List<ClaudeProjectInfo> projects = [];
    private readonly FileSystemWatcher? _fileWatcher;
    private readonly Subject<FileSystemEventArgs> _fileChangedSubject;

    public IReadOnlyList<ClaudeProjectInfo> Projects => this.projects;

    public IObservable<IReadOnlyList<ClaudeProjectInfo>> ProjectsObservable { get; }

    public ClaudeDataService()
    {
        this._fileChangedSubject = new Subject<FileSystemEventArgs>();

        // Create file watcher for the projects directory only if it exists
        if (Directory.Exists(ProjectDir))
        {
            this._fileWatcher = new FileSystemWatcher(ProjectDir)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName,
                EnableRaisingEvents = true,
            };

            // Wire up file system events
            this._fileWatcher.Created += this.OnFileChanged;
            this._fileWatcher.Changed += this.OnFileChanged;
            this._fileWatcher.Deleted += this.OnFileChanged;
            this._fileWatcher.Renamed += this.OnFileChanged;
        }

        // Create observable that debounces file changes and reloads projects
        var fileChangeObservable = this._fileChangedSubject
                                       .Throttle(TimeSpan.FromMilliseconds(500)) // Debounce rapid file changes
                                       .SelectMany(_ => this.LoadProjectsAsync());

        var initialLoadObservable = Observable.FromAsync(this.LoadProjectsAsync);

        this.ProjectsObservable = initialLoadObservable.Merge(fileChangeObservable);
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        this._fileChangedSubject.OnNext(e);
    }

    public async Task<IReadOnlyList<ClaudeProjectInfo>> LoadProjectsAsync()
    {
        if (!Directory.Exists(ProjectDir))
        {
            return new List<ClaudeProjectInfo>();
        }

        var claudeProjects = Directory.GetDirectories(ProjectDir);
        var wg = new WaitGroup();
        var ch = new DefaultChannel<ClaudeSessionMetadata>();

        foreach (var session in claudeProjects.SelectMany(Directory.EnumerateFiles))
        {
            Go(wg, async () =>
            {
                var lines = File.ReadLines(session);
                var firstLine = lines.FirstOrDefault();

                if (firstLine is null)
                {
                    return;
                }

                // Last line query requires O(n), which drastically increases the startup time.
                // var lastLine = lines.Last();

                var metadata = JsonSerializer.Deserialize<ClaudeSessionMetadata>(firstLine);
                // var lastLineMetadata = JsonSerializer.Deserialize<ClaudeSessionMetadata>(lastLine);

                if (metadata is null)
                {
                    return;
                }

                await ch.WriteAsync(metadata);
            });
        }

        Go(async () =>
        {
            await wg.WaitAsync();
            await ch.CompleteAsync();
        });

        var sessions = await ch.Where(s => s.Cwd is not null)
                               .OrderBy(s => s.Cwd)
                               .ToListAsync();

        this.projects = sessions.GroupBy(s => s.Cwd)
                                .Select(ClaudeProjectInfo.From!)
                                .ToList();

        return this.projects;
    }

    public static bool ClaudeDirectoryExists()
    {
        var homeDirectory = Environment.GetEnvironmentVariable("HOME")!;
        var claudeDirectory = Path.Combine(homeDirectory, ".claude");
        var projectDir = Path.Combine(claudeDirectory, "projects");

        return Directory.Exists(projectDir);
    }

    public static string GetClaudeProjectsPath()
    {
        var homeDirectory = Environment.GetEnvironmentVariable("HOME")!;
        var claudeDirectory = Path.Combine(homeDirectory, ".claude");

        return Path.Combine(claudeDirectory, "projects");
    }

    public void Dispose()
    {
        this._fileWatcher?.Dispose();
        this._fileChangedSubject.Dispose();
    }
}