namespace Clauder.Services;

using Clauder.Abstractions;
using Spectre.Console;
using Spectre.Console.Rendering;

public sealed class LayoutManager : ILayoutManager
{
    private readonly IToastContext _toastContext;
    private bool _disposed;

    public LayoutManager(IToastContext toastContext)
    {
        this._toastContext = toastContext;
    }

    public async Task<IRenderable> CreateLayoutAsync(IPage page, IRenderable? toastDisplay = null, CancellationToken cancellationToken = default)
    {
        this.ThrowIfDisposed();

        var header = await this.SafeRenderFragmentAsync(
            async () =>
            {
                var content = await page.RenderHeaderAsync();
                return new Padder(content, new Padding(1, 1, 1, 0));
            },
            "Header",
            () => new Markup("[red]Header rendering failed[/]"));

        var body = await this.SafeRenderFragmentAsync(
            async () =>
            {
                var content = await page.RenderBodyAsync();

                return new Padder(content, new Padding(1, 1, 1, 1));
            },
            "Body",
            () => new Markup("[red]Content rendering failed[/]"));

        var footer = await this.SafeRenderFragmentAsync(
            async () =>
            {
                var content = await page.RenderFooterAsync();
                
                return new Padder(content, new Padding(1, 0, 1, 1));
            },
            "Footer",
            () => new Markup("[red]Footer rendering failed[/]"));

        var errorDisplay = toastDisplay ?? new Markup(string.Empty);

        var layout = new Layout("Root")
            .SplitRows(
                new Layout("Header").Size(4),
                new Layout("Content"),
                new Layout("Footer")
                    .Size(4)
                    .SplitColumns(
                        new Layout("FooterMain").Ratio(70),
                        new Layout("FooterError").Ratio(30)
                    )
            );

        layout["Header"].Update(header);
        layout["Content"].Update(body);
        layout["FooterMain"].Update(footer);
        layout["FooterError"].Update(errorDisplay);

        return layout;
    }

    private async Task<IRenderable> SafeRenderFragmentAsync(
        Func<ValueTask<IRenderable>> renderFunc,
        string fragmentName,
        Func<IRenderable> fallbackRenderer)
    {
        try
        {
            return await renderFunc();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fragment rendering failed ({fragmentName}): {ex.Message}");

            try
            {
                await this._toastContext.ShowErrorAsync(ex.Message);
            }
            catch
            {
                // Ignore toast errors during error handling
            }

            return fallbackRenderer();
        }
    }

    private void ThrowIfDisposed()
    {
        if (this._disposed)
            throw new ObjectDisposedException(nameof(LayoutManager));
    }

    public void Dispose()
    {
        if (this._disposed)
            return;

        this._disposed = true;
    }
}