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

    private IReadOnlyList<ClaudeProjectSummary> projectSummaries = [];
    private readonly Dictionary<string, ClaudeProjectInfo> _projectCache = new();
    private readonly FileSystemWatcher? _fileWatcher;
    private readonly Subject<FileSystemEventArgs> _fileChangedSubject;

    public IReadOnlyList<ClaudeProjectSummary> ProjectSummaries => this.projectSummaries;

    public IObservable<IReadOnlyList<ClaudeProjectSummary>> ProjectSummariesObservable { get; }

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

        // Create observable that debounces file changes and reloads project summaries
        var fileChangeObservable = this._fileChangedSubject
                                       .Throttle(TimeSpan.FromMilliseconds(500)) // Debounce rapid file changes
                                       .SelectMany(_ => this.LoadProjectSummariesAsync());

        var initialLoadObservable = Observable.FromAsync(this.LoadProjectSummariesAsync);

        this.ProjectSummariesObservable = initialLoadObservable.Merge(fileChangeObservable);
    }

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        this._projectCache.Clear();
        this._fileChangedSubject.OnNext(e);
    }

    public async Task<IReadOnlyList<ClaudeProjectSummary>> LoadProjectSummariesAsync()
    {
        if (!Directory.Exists(ProjectDir))
        {
            return [];
        }

        var claudeProjectDirectories = Directory.GetDirectories(ProjectDir);

        var wg = new WaitGroup();
        var ch = new DefaultChannel<ClaudeProjectSummary>();

        foreach (var projectDirectory in claudeProjectDirectories)
        {
            Go(wg, async () =>
            {
                var project = ClaudeProjectSummary.FromDirectory(projectDirectory);

                await ch.WriteAsync(project);
            });
        }

        Go(async () =>
        {
            await wg.WaitAsync();
            await ch.CompleteAsync();
        });

        var sortedProjects = await ch
                                      .OrderBy(p => p.ProjectName)
                                      .ToListAsync();
        this.projectSummaries = sortedProjects;

        return this.projectSummaries;
    }

    public async Task<ClaudeProjectInfo> LoadProjectSessionsAsync(ClaudeProjectSummary project)
    {
        var projectPath = project.ProjectPath;

        if (this._projectCache.TryGetValue(projectPath, out var cachedProject))
        {
            return cachedProject;
        }

        var projectDirectoryName = projectPath.Replace(Path.DirectorySeparatorChar, '-');
        var projectDirectory = Path.Combine(ProjectDir, projectDirectoryName);

        if (!Directory.Exists(projectDirectory))
        {
            throw new DirectoryNotFoundException($"Project directory not found: {projectDirectory}");
        }

        var sessionFiles = Directory.GetFiles(projectDirectory, "*.jsonl");
        var wg = new WaitGroup();
        var ch = new DefaultChannel<ClaudeSessionMetadata>();

        foreach (var sessionFile in sessionFiles)
        {
            Go(wg, async () =>
            {
                var lines = File.ReadLines(sessionFile);
                var firstLine = lines.FirstOrDefault();

                if (firstLine is null)
                {
                    return;
                }

                var metadata = JsonSerializer.Deserialize<ClaudeSessionMetadata>(firstLine);

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
                               .OrderBy(s => s.Timestamp)
                               .ToListAsync();

        var groupedSessions = sessions.GroupBy(s => s.Cwd!).First();
        var projectInfo = ClaudeProjectInfo.From(groupedSessions);

        this._projectCache[projectPath] = projectInfo;

        return projectInfo;
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