using Clauder.Abstractions;
using Clauder.Services;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Clauder.Tests.Services;

public class PageFactoryTests
{
    private readonly ServiceCollection _services;
    private readonly PageFactory _pageFactory;

    public PageFactoryTests()
    {
        this._services = new ServiceCollection();
        this._services.AddSingleton<TestDependency>();

        var serviceProvider = this._services.BuildServiceProvider();
        this._pageFactory = new PageFactory(serviceProvider);
    }

    [Fact]
    public void CreatePage_WithDefaultConstructor_ShouldCreateInstance()
    {
        var page = this._pageFactory.CreatePage<TestPageWithDefaultConstructor>();

        page.Should().NotBeNull();
        page.Should().BeOfType<TestPageWithDefaultConstructor>();
    }

    [Fact]
    public void CreatePage_WithDependencyInjection_ShouldInjectDependencies()
    {
        var page = this._pageFactory.CreatePage<TestPageWithDependency>();

        page.Should().NotBeNull();
        page.Should().BeOfType<TestPageWithDependency>();
        page.Dependency.Should().NotBeNull();
    }

    [Fact]
    public void CreatePage_WithProvidedParameters_ShouldUseParameters()
    {
        var testString = "test parameter";
        var testInt = 42;

        var page = this._pageFactory.CreatePage<TestPageWithParameters>(testString, testInt);

        page.Should().NotBeNull();
        page.Should().BeOfType<TestPageWithParameters>();
        page.StringParam.Should().Be(testString);
        page.IntParam.Should().Be(testInt);
    }

    [Fact]
    public void CreatePage_WithMixedParameters_ShouldUseBothProvidedAndInjected()
    {
        var testString = "test parameter";

        var page = this._pageFactory.CreatePage<TestPageWithMixedParameters>(testString);

        page.Should().NotBeNull();
        page.Should().BeOfType<TestPageWithMixedParameters>();
        page.StringParam.Should().Be(testString);
        page.Dependency.Should().NotBeNull();
    }

    [Fact]
    public void CreatePage_WithUnresolvableDependency_ShouldThrow()
    {
        var act = () => this._pageFactory.CreatePage<TestPageWithUnresolvableDependency>();

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*cannot be resolved from the service container*");
    }

    [Fact]
    public void CreatePage_WithMultipleConstructors_ShouldChooseBestMatch()
    {
        const string testString = "test parameter";

        var page = this._pageFactory.CreatePage<TestPageWithMultipleConstructors>(testString);

        page.Should().NotBeNull();
        page.Should().BeOfType<TestPageWithMultipleConstructors>();
        page.StringParam.Should().Be(testString);
        page.Dependency.Should().NotBeNull();
        page.UsedParameterizedConstructor.Should().BeTrue();
    }

    // Test classes
    public class TestDependency
    {
        public string Value { get; set; } = "Injected";
    }

    public class UnresolvableDependency
    {
    }

    public class TestPageWithDefaultConstructor : IPage
    {
        public string Title => "Test Page";
        
        public ValueTask<IRenderable> RenderHeaderAsync() => ValueTask.FromResult<IRenderable>(new Markup("Header"));

        public ValueTask<IRenderable> RenderBodyAsync() => ValueTask.FromResult<IRenderable>(new Markup("Body"));

        public ValueTask<IRenderable> RenderFooterAsync() => ValueTask.FromResult<IRenderable>(new Markup("Footer"));

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public void Dispose()
        {
        }
    }

    public class TestPageWithDependency : IPage
    {
        public TestDependency Dependency { get; }

        public string Title => "Test Page With Dependency";

        public TestPageWithDependency(TestDependency dependency)
        {
            this.Dependency = dependency;
        }

        public Task DisplayAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask<IRenderable> RenderHeaderAsync() => ValueTask.FromResult<IRenderable>(new Markup("Header"));

        public ValueTask<IRenderable> RenderBodyAsync() => ValueTask.FromResult<IRenderable>(new Markup("Body"));

        public ValueTask<IRenderable> RenderFooterAsync() => ValueTask.FromResult<IRenderable>(new Markup("Footer"));

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public void Dispose()
        {
        }
    }

    public class TestPageWithParameters : IPage
    {
        public string StringParam { get; }

        public int IntParam { get; }

        public string Title => "Test Page With Parameters";

        public TestPageWithParameters(string stringParam, int intParam)
        {
            this.StringParam = stringParam;
            this.IntParam = intParam;
        }

        public Task DisplayAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask<IRenderable> RenderHeaderAsync() => ValueTask.FromResult<IRenderable>(new Markup("Header"));

        public ValueTask<IRenderable> RenderBodyAsync() => ValueTask.FromResult<IRenderable>(new Markup("Body"));

        public ValueTask<IRenderable> RenderFooterAsync() => ValueTask.FromResult<IRenderable>(new Markup("Footer"));

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public void Dispose()
        {
        }
    }

    public class TestPageWithMixedParameters : IPage
    {
        public string StringParam { get; }

        public TestDependency Dependency { get; }

        public string Title => "Test Page With Mixed Parameters";

        public TestPageWithMixedParameters(string stringParam, TestDependency dependency)
        {
            this.StringParam = stringParam;
            this.Dependency = dependency;
        }

        public Task DisplayAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask<IRenderable> RenderHeaderAsync() => ValueTask.FromResult<IRenderable>(new Markup("Header"));

        public ValueTask<IRenderable> RenderBodyAsync() => ValueTask.FromResult<IRenderable>(new Markup("Body"));

        public ValueTask<IRenderable> RenderFooterAsync() => ValueTask.FromResult<IRenderable>(new Markup("Footer"));

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public void Dispose()
        {
        }
    }

    public class TestPageWithUnresolvableDependency : IPage
    {
        public UnresolvableDependency Dependency { get; }

        public string Title => "Test Page With Unresolvable Dependency";

        public TestPageWithUnresolvableDependency(UnresolvableDependency dependency)
        {
            this.Dependency = dependency;
        }

        public Task DisplayAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask<IRenderable> RenderHeaderAsync() => ValueTask.FromResult<IRenderable>(new Markup("Header"));

        public ValueTask<IRenderable> RenderBodyAsync() => ValueTask.FromResult<IRenderable>(new Markup("Body"));

        public ValueTask<IRenderable> RenderFooterAsync() => ValueTask.FromResult<IRenderable>(new Markup("Footer"));

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public void Dispose()
        {
        }
    }

    public class TestPageWithMultipleConstructors : IPage
    {
        public string? StringParam { get; }

        public TestDependency Dependency { get; }

        public bool UsedParameterizedConstructor { get; }

        public string Title => "Test Page With Multiple Constructors";

        public TestPageWithMultipleConstructors(TestDependency dependency)
        {
            this.Dependency = dependency;
            this.UsedParameterizedConstructor = false;
        }

        public TestPageWithMultipleConstructors(string stringParam, TestDependency dependency)
        {
            this.StringParam = stringParam;
            this.Dependency = dependency;
            this.UsedParameterizedConstructor = true;
        }

        public Task DisplayAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public ValueTask<IRenderable> RenderHeaderAsync() => ValueTask.FromResult<IRenderable>(new Markup("Header"));

        public ValueTask<IRenderable> RenderBodyAsync() => ValueTask.FromResult<IRenderable>(new Markup("Body"));

        public ValueTask<IRenderable> RenderFooterAsync() => ValueTask.FromResult<IRenderable>(new Markup("Footer"));

        public ValueTask InitializeAsync(CancellationToken cancellationToken = default) => ValueTask.CompletedTask;

        public void Dispose()
        {
        }
    }
}