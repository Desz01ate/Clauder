using System.Reflection;
using System.Collections.Concurrent;
using Clauder.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Clauder.Services;

public class PageFactory : IPageFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ConcurrentDictionary<Type, PageTypeMetadata> _pageTypeCache = new();

    private sealed record ConstructorMetadata(
        ConstructorInfo Constructor,
        ParameterInfo[] Parameters,
        Type[] ParameterTypes,
        string[] ParameterNames);

    private sealed record PageTypeMetadata(
        Type PageType,
        ConstructorMetadata[] Constructors);

    public PageFactory(IServiceProvider serviceProvider)
    {
        this._serviceProvider = serviceProvider;
    }

    public T CreatePage<T>(params object[] parameters)
        where T : class, IPage
    {
        var pageType = typeof(T);

        if (pageType is { IsInterface: true } or { IsAbstract: true })
        {
            throw new ArgumentException($"Type {pageType.Name} is an interface or abstract class.", nameof(pageType));
        }

        // Get cached page metadata
        var metadata = _pageTypeCache.GetOrAdd(pageType, BuildPageTypeMetadata);

        // Find the best matching constructor
        var bestConstructor = FindBestConstructor(metadata.Constructors, parameters);

        if (bestConstructor == null)
        {
            throw new InvalidOperationException($"No suitable constructor found for {pageType.Name} with the provided parameters.");
        }

        // Get constructor parameters (cached)
        var constructorParams = bestConstructor.Parameters;
        var parameterTypes = bestConstructor.ParameterTypes;
        var parameterNames = bestConstructor.ParameterNames;
        var args = new object[constructorParams.Length];

        // Track which provided parameters we've used
        var usedParameterIndices = new HashSet<int>();

        // Fill constructor arguments
        for (var i = 0; i < constructorParams.Length; i++)
        {
            var paramType = parameterTypes[i];
            var paramName = parameterNames[i];

            // First, try to match with provided parameters by type
            var matchingParamIndex = FindMatchingParameter(parameters, paramType, usedParameterIndices);

            if (matchingParamIndex != -1)
            {
                args[i] = parameters[matchingParamIndex];
                usedParameterIndices.Add(matchingParamIndex);
            }
            else
            {
                // If no matching parameter provided, try to resolve from DI container
                try
                {
                    args[i] = this._serviceProvider.GetRequiredService(paramType);
                }
                catch (InvalidOperationException)
                {
                    throw new InvalidOperationException(
                        $"Cannot create {pageType.Name}: parameter '{paramName}' of type {paramType.Name} " +
                        "was not provided and cannot be resolved from the service container.");
                }
            }
        }

        return (T)bestConstructor.Constructor.Invoke(args)!;
    }

    public IPage CreatePage(Type pageType, params object[] parameters)
    {
        if (pageType is { IsInterface: true } or { IsAbstract: true })
        {
            throw new ArgumentException($"Type {pageType.Name} is an interface or abstract class.", nameof(pageType));
        }

        if (!typeof(IPage).IsAssignableFrom(pageType))
        {
            throw new ArgumentException($"Type {pageType.Name} does not implement IDisplay.", nameof(pageType));
        }

        // Get cached page metadata
        var metadata = _pageTypeCache.GetOrAdd(pageType, BuildPageTypeMetadata);

        // Find the best matching constructor
        var bestConstructor = FindBestConstructor(metadata.Constructors, parameters);

        if (bestConstructor == null)
        {
            throw new InvalidOperationException($"No suitable constructor found for {pageType.Name} with the provided parameters.");
        }

        // Get constructor parameters (cached)
        var constructorParams = bestConstructor.Parameters;
        var parameterTypes = bestConstructor.ParameterTypes;
        var parameterNames = bestConstructor.ParameterNames;
        var args = new object[constructorParams.Length];

        // Track which provided parameters we've used
        var usedParameterIndices = new HashSet<int>();

        // Fill constructor arguments
        for (var i = 0; i < constructorParams.Length; i++)
        {
            var paramType = parameterTypes[i];
            var paramName = parameterNames[i];

            // First, try to match with provided parameters by type
            var matchingParamIndex = FindMatchingParameter(parameters, paramType, usedParameterIndices);

            if (matchingParamIndex != -1)
            {
                args[i] = parameters[matchingParamIndex];
                usedParameterIndices.Add(matchingParamIndex);
            }
            else
            {
                // If no matching parameter provided, try to resolve from DI container
                try
                {
                    args[i] = this._serviceProvider.GetRequiredService(paramType);
                }
                catch (InvalidOperationException)
                {
                    throw new InvalidOperationException(
                        $"Cannot create {pageType.Name}: parameter '{paramName}' of type {paramType.Name} " +
                        "was not provided and cannot be resolved from the service container.");
                }
            }
        }

        return (IPage)bestConstructor.Constructor.Invoke(args)!;
    }

    private static PageTypeMetadata BuildPageTypeMetadata(Type pageType)
    {
        var constructors = pageType.GetConstructors();
        var constructorMetadata = new ConstructorMetadata[constructors.Length];

        for (var i = 0; i < constructors.Length; i++)
        {
            var constructor = constructors[i];
            var parameters = constructor.GetParameters();
            var parameterTypes = new Type[parameters.Length];
            var parameterNames = new string[parameters.Length];

            for (var j = 0; j < parameters.Length; j++)
            {
                parameterTypes[j] = parameters[j].ParameterType;
                parameterNames[j] = parameters[j].Name ?? string.Empty;
            }

            constructorMetadata[i] = new ConstructorMetadata(constructor, parameters, parameterTypes, parameterNames);
        }

        return new PageTypeMetadata(pageType, constructorMetadata);
    }

    private static ConstructorMetadata? FindBestConstructor(ConstructorMetadata[] constructors, object[] parameters)
    {
        // Prefer constructors that can use all provided parameters
        var parameterTypes = parameters.Select(p => p.GetType()).ToArray();

        return constructors
               .Where(c => CanConstructorUseParameters(c, parameterTypes))
               .OrderByDescending(c => c.ParameterTypes.Length)
               .FirstOrDefault();
    }

    private static bool CanConstructorUseParameters(ConstructorMetadata constructor, Type[] parameterTypes)
    {
        var constructorParams = constructor.ParameterTypes;
        var usedTypes = new HashSet<Type>();

        foreach (var paramType in parameterTypes)
        {
            var matchingParam = constructorParams.FirstOrDefault(p =>
                IsAssignableFrom(p, paramType) && !usedTypes.Contains(p));

            if (matchingParam != null)
            {
                usedTypes.Add(matchingParam);
            }
        }

        return true; // We can always try to resolve missing parameters from DI
    }

    private static int FindMatchingParameter(object[] parameters, Type targetType, HashSet<int> usedIndices)
    {
        for (var i = 0; i < parameters.Length; i++)
        {
            if (usedIndices.Contains(i))
                continue;

            if (IsAssignableFrom(targetType, parameters[i].GetType()))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsAssignableFrom(Type targetType, Type sourceType)
    {
        return targetType.IsAssignableFrom(sourceType);
    }
}