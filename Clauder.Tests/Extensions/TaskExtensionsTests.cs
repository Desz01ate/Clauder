using Clauder.Extensions;
using Concur.Implementations;
using FluentAssertions;

namespace Clauder.Tests.Extensions;

public class TaskExtensionsTests
{
    [Fact]
    public async Task WriteTo_WithTaskOfT_ShouldWriteResultToChannel()
    {
        var channel = new DefaultChannel<string>();
        const string testValue = "test result";
        var task = Task.FromResult(testValue);

        await task.WriteTo(channel);
        await channel.CompleteAsync();

        var result = await channel.FirstAsync();
        result.Should().Be(testValue);
    }

    [Fact]
    public async Task WriteTo_WithValueTaskOfT_ShouldWriteResultToChannel()
    {
        var channel = new DefaultChannel<int>();
        var testValue = 42;
        var valueTask = new ValueTask<int>(testValue);

        await valueTask.WriteTo(channel);
        await channel.CompleteAsync();

        var result = await channel.FirstAsync();
        result.Should().Be(testValue);
    }

    [Fact]
    public async Task WriteTo_WithDelayedTask_ShouldWaitAndWriteResult()
    {
        var channel = new DefaultChannel<string>();
        var testValue = "delayed result";

        var task = Task.Run(async () =>
        {
            await Task.Delay(10);
            return testValue;
        });

        await task.WriteTo(channel);
        await channel.CompleteAsync();

        var result = await channel.FirstAsync();
        result.Should().Be(testValue);
    }

    [Fact]
    public async Task WriteTo_WithMultipleTasks_ShouldWriteAllResults()
    {
        var channel = new DefaultChannel<int>();
        var values = new[] { 1, 2, 3, 4, 5 };
        var tasks = values.Select(v => Task.FromResult(v));

        foreach (var task in tasks)
        {
            await task.WriteTo(channel);
        }

        await channel.CompleteAsync();

        var results = new List<int>();

        await foreach (var result in channel)
        {
            results.Add(result);
        }

        results.Should().BeEquivalentTo(values);
    }

    [Fact]
    public async Task WriteTo_WithTaskThatThrows_ShouldPropagateException()
    {
        var channel = new DefaultChannel<string>();
        var task = Task.FromException<string>(new InvalidOperationException("Test exception"));

        var act = async () => await task.WriteTo(channel);

        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("Test exception");
    }

    [Fact]
    public async Task WriteTo_WithValueTaskThatThrows_ShouldPropagateException()
    {
        var channel = new DefaultChannel<string>();
        var valueTask = new ValueTask<string>(Task.FromException<string>(new ArgumentException("Value task exception")));

        var act = async () => await valueTask.WriteTo(channel);

        await act.Should().ThrowAsync<ArgumentException>()
                 .WithMessage("Value task exception");
    }

    [Fact]
    public async Task WriteTo_WithComplexObject_ShouldWriteCorrectly()
    {
        var channel = new DefaultChannel<TestObject>();
        var testObject = new TestObject { Id = 123, Name = "Test Object" };
        var task = Task.FromResult(testObject);

        await task.WriteTo(channel);
        await channel.CompleteAsync();

        var result = await channel.FirstAsync();
        result.Should().NotBeNull();
        result.Should().BeEquivalentTo(testObject);
    }

    [Fact]
    public async Task WriteTo_WithNullResult_ShouldWriteNullToChannel()
    {
        var channel = new DefaultChannel<string?>();
        var task = Task.FromResult<string?>(null);

        await task.WriteTo(channel);
        await channel.CompleteAsync();

        var result = await channel.FirstAsync();
        result.Should().BeNull();
    }

    private class TestObject
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;
    }
}