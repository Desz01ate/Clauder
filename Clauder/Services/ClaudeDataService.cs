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
    private readonly ClaudeConfiguration _configuration;

    private readonly Dictionary<string, ClaudeProjectInfo> _projectCache = new();
    private readonly FileSystemWatcher? _fileWatcher;
    private readonly Subject<FileSystemEventArgs> _fileChangedSubject;

    public IReadOnlyList<ClaudeProjectSummary> ProjectSummaries { get; private set; } = [];

    public IObservable<IReadOnlyList<ClaudeProjectSummary>> ProjectSummariesObservable { get; }

    public ClaudeDataService(ClaudeConfiguration configuration)
    {
        this._configuration = configuration;
        this._fileChangedSubject = new Subject<FileSystemEventArgs>();

        // Create file watcher for the projects directory only if it exists
        if (Directory.Exists(this._configuration.ClaudeProjectDirectory))
        {
            this._fileWatcher = new FileSystemWatcher(this._configuration.ClaudeProjectDirectory)
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
        if (!Directory.Exists(this._configuration.ClaudeProjectDirectory))
        {
            return [];
        }

        var claudeProjectDirectories = Directory.GetDirectories(this._configuration.ClaudeProjectDirectory);

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
        this.ProjectSummaries = sortedProjects;

        return this.ProjectSummaries;
    }

    public async Task<ClaudeProjectInfo> LoadProjectSessionsAsync(ClaudeProjectSummary project)
    {
        var projectPath = project.ProjectPath;

        if (this._projectCache.TryGetValue(projectPath, out var cachedProject))
        {
            return cachedProject;
        }

        var projectDirectoryName = projectPath.Replace(Path.DirectorySeparatorChar, '-');
        var projectDirectory = Path.Combine(this._configuration.ClaudeProjectDirectory, projectDirectoryName);

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
                    // Create placeholder metadata for empty files
                    var emptyFileMetadata = CreatePlaceholderMetadata(sessionFile, projectPath);
                    await ch.WriteAsync(emptyFileMetadata);
                    return;
                }

                try
                {
                    var metadata = JsonSerializer.Deserialize<ClaudeSessionMetadata>(firstLine);

                    if (metadata is null)
                    {
                        // Create placeholder metadata for null deserialization
                        var nullMetadata = CreatePlaceholderMetadata(sessionFile, projectPath);
                        await ch.WriteAsync(nullMetadata);
                        return;
                    }

                    // Ensure Cwd is set for valid metadata
                    if (string.IsNullOrEmpty(metadata.Cwd))
                    {
                        metadata = metadata with { Cwd = projectPath };
                    }

                    await ch.WriteAsync(metadata);
                }
                catch
                {
                    // Create placeholder metadata for corrupted files
                    var corruptedMetadata = CreatePlaceholderMetadata(sessionFile, projectPath);
                    await ch.WriteAsync(corruptedMetadata);
                }
            });
        }

        Go(async () =>
        {
            await wg.WaitAsync();
            await ch.CompleteAsync();
        });

        var sessions = await ch.OrderBy(s => s.Timestamp)
                               .ToListAsync();
        
        var projectInfo = ClaudeProjectInfo.From(sessions);

        this._projectCache[projectPath] = projectInfo;

        return projectInfo;
    }

    private record struct FileHeaderInfo(
        string Type,
        string Content);

    private static ClaudeSessionMetadata CreatePlaceholderMetadata(string sessionFile, string projectPath)
    {
        var fileInfo = new FileInfo(sessionFile);
        var sessionId = Path.GetFileNameWithoutExtension(sessionFile);

        var info = GetFileHeader(sessionFile);
        var type = info.Type;

        return new ClaudeSessionMetadata
        {
            SessionId = sessionId,
            Cwd = projectPath,
            Timestamp = fileInfo.LastWriteTime,
            Message = new Message
            {
                Role = "system",
                Content = info.Content,
            },
            Uuid = Guid.NewGuid().ToString(),
            UserType = "system",
            Type = type,
            IsMeta = true,
            IsSidechain = false,
            ParentUuid = null,
            Version = null,
            GitBranch = null,
        };
    }

    private static FileHeaderInfo GetFileHeader(string sessionFile)
    {
        try
        {
            var firstLine = File.ReadLines(sessionFile).FirstOrDefault();

            if (firstLine == null)
            {
                return new FileHeaderInfo("corrupted", string.Empty);
            }

            // Try to parse as AI summary format first
            using var document = JsonDocument.Parse(firstLine);
            var root = document.RootElement;

            var type = root.TryGetProperty("type", out var typeProperty) ? typeProperty.GetString()! : "corrupted";

            return new FileHeaderInfo(type, firstLine);
        }
        catch
        {
            return new FileHeaderInfo("corrupted", string.Empty);
        }
    }

    public static bool ClaudeDirectoryExists(ClaudeConfiguration configuration)
    {
        var projectDir = configuration.ClaudeProjectDirectory;

        return Directory.Exists(projectDir);
    }

    public void Dispose()
    {
        this._fileWatcher?.Dispose();
        this._fileChangedSubject.Dispose();
    }
}