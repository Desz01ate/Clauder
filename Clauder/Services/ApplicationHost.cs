namespace Clauder.Services;

using System.Threading.Channels;
using Clauder.Abstractions;
using Clauder.Models;
using Clauder.Pages;
using Commands;
using Concur;
using static Concur.ConcurRoutine;

public sealed class ApplicationHost : IApplicationHost
{
    private readonly ChannelReader<NavigationCommand> _navigationReader;
    private readonly ChannelReader<ToastCommand> _toastReader;
    private readonly IRenderEngine _renderEngine;
    private readonly IPageManager _pageManager;
    private readonly IToastManager _toastManager;
    private readonly IInputProcessor _inputProcessor;
    private readonly ILayoutManager _layoutManager;
    private readonly IToastContext _toastContext;

    private bool _disposed;
    private CancellationTokenSource? _runCancellation;
    private CancellationTokenSource? _currentPageCancellation;
    private Action? _refreshCallback;

    public ApplicationHost(
        ChannelReader<NavigationCommand> navigationReader,
        ChannelReader<ToastCommand> toastReader,
        IRenderEngine renderEngine,
        IPageManager pageManager,
        IToastManager toastManager,
        IInputProcessor inputProcessor,
        ILayoutManager layoutManager,
        IToastContext toastContext)
    {
        this._navigationReader = navigationReader;
        this._toastReader = toastReader;
        this._renderEngine = renderEngine;
        this._pageManager = pageManager;
        this._toastManager = toastManager;
        this._inputProcessor = inputProcessor;
        this._layoutManager = layoutManager;
        this._toastContext = toastContext;

        this._pageManager.PageChanged += this.OnPageChanged;
        this._toastManager.ToastDisplayChanged += this.OnToastDisplayChanged;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();

        this._runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var cts = this._runCancellation;

        try
        {
            // Initialize with the first page
            await this._pageManager.NavigateToAsync(
                new NavigateToCommand(typeof(ProjectsPage), []),
                cts.Token);

            var wg = new WaitGroup();

            // Start all concurrent operations
            Go(wg, () => this.RunRenderingLoopAsync(cts));
            Go(wg, () => this.ProcessNavigationCommandsAsync(cts));
            Go(wg, () => this.ProcessToastCommandsAsync(cts));
            Go(wg, () => this._toastManager.StartAsync(cts.Token));

            await wg.WaitAsync();
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
    }

    private async Task RunRenderingLoopAsync(CancellationTokenSource cts)
    {
        var cancellationToken = cts.Token;

        while (!cancellationToken.IsCancellationRequested && this._pageManager.HasPages)
        {
            var currentPage = this._pageManager.CurrentPage;

            if (currentPage == null)
            {
                await Task.Delay(50, cancellationToken);
                continue;
            }

            this._currentPageCancellation?.Dispose();
            this._currentPageCancellation = new CancellationTokenSource();

            try
            {
                using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    this._currentPageCancellation.Token);

                await this.RenderPageAsync(currentPage, combinedCts.Token);
            }
            catch (OperationCanceledException)
            {
                // Navigation occurred or cancellation requested
                continue;
            }
            catch (Exception ex)
            {
                await this.HandleRenderingErrorAsync(currentPage, ex);
                await Task.Delay(1000, cancellationToken);
            }
        }

        await cts.CancelAsync();
    }

    private async Task RenderPageAsync(IPage page, CancellationToken cancellationToken)
    {
        Console.Title = page.Title;

        try
        {
            await page.InitializeAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Page initialization failed: {ex.Message}");
            await this._toastContext.ShowErrorAsync(ex.Message);
        }

        // Check if this page needs full console control
        if (page is IFullConsoleControlPage fullControlPage)
        {
            // Hand over full control to the page
            await fullControlPage.RunAsync(cancellationToken);
            await this._pageManager.NavigateBackAsync(cancellationToken);

            return;
        }

        var initialLayout = await this._layoutManager.CreateLayoutAsync(page, this._toastManager.CurrentToastDisplay, cancellationToken);

        await this._renderEngine.RunRenderLoopAsync(
            initialLayout,
            async () => await this._layoutManager.CreateLayoutAsync(page, this._toastManager.CurrentToastDisplay, cancellationToken),
            async keyInfo => await this._inputProcessor.ProcessInputAsync(keyInfo, page, cancellationToken),
            refreshCallback => this._refreshCallback = refreshCallback,
            cancellationToken);
    }

    private async Task ProcessNavigationCommandsAsync(CancellationTokenSource cts)
    {
        var cancellationToken = cts.Token;

        try
        {
            await foreach (var command in this._navigationReader.ReadAllAsync(cancellationToken))
            {
                switch (command)
                {
                    case NavigateToCommand navigateCommand:
                        await this._toastContext.ClearAsync();
                        await this._pageManager.NavigateToAsync(navigateCommand, cancellationToken);
                        break;

                    case NavigateBackCommand:
                        await this._toastContext.ClearAsync();
                        await this._pageManager.NavigateBackAsync(cancellationToken);
                        break;

                    case ExitCommand:
                        return;

                    default:
                        await this._toastContext.ShowErrorAsync($"Unknown navigation command: {command.GetType().Name}");
                        break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }

        await cts.CancelAsync();
    }

    private async Task ProcessToastCommandsAsync(CancellationTokenSource cts)
    {
        var cancellationToken = cts.Token;

        try
        {
            await foreach (var command in this._toastReader.ReadAllAsync(cancellationToken))
            {
                await this._toastManager.ProcessToastCommandAsync(command, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }

        await cts.CancelAsync();
    }

    private async Task HandleRenderingErrorAsync(IPage currentPage, Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"Rendering error for page {currentPage.GetType().Name}: {ex.Message}");
        await this._toastContext.ShowErrorAsync(ex.Message);
    }

    private void OnPageChanged(object? sender, PageChangedEventArgs e)
    {
        // Cancel current page rendering to allow navigation
        this._currentPageCancellation?.Cancel();
    }

    private void OnToastDisplayChanged(object? sender, ToastDisplayChangedEventArgs e)
    {
        // Trigger immediate refresh when toast display changes
        this._refreshCallback?.Invoke();
    }

    private void ThrowIfDisposed()
    {
        if (this._disposed)
            throw new ObjectDisposedException(nameof(ApplicationHost));
    }

    public void Dispose()
    {
        if (this._disposed)
            return;

        this._disposed = true;

        this._runCancellation?.Cancel();
        this._runCancellation?.Dispose();

        this._currentPageCancellation?.Cancel();
        this._currentPageCancellation?.Dispose();

        this._pageManager.PageChanged -= this.OnPageChanged;
        this._toastManager.ToastDisplayChanged -= this.OnToastDisplayChanged;

        this._renderEngine.Dispose();
        this._pageManager.Dispose();
        this._toastManager.Dispose();
        this._inputProcessor.Dispose();
        this._layoutManager.Dispose();
    }
}