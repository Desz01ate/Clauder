using System.Reflection;
using Clauder.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Clauder.Services;

public class PageFactory : IPageFactory
{
    private readonly IServiceProvider _serviceProvider;

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

        // Get all constructors for the page type
        var constructors = pageType.GetConstructors();

        // Find the best matching constructor
        var bestConstructor = FindBestConstructor(constructors, parameters);

        if (bestConstructor == null)
        {
            throw new InvalidOperationException($"No suitable constructor found for {pageType.Name} with the provided parameters.");
        }

        // Get constructor parameters
        var constructorParams = bestConstructor.GetParameters();
        var args = new object[constructorParams.Length];

        // Track which provided parameters we've used
        var usedParameterIndices = new HashSet<int>();

        // Fill constructor arguments
        for (var i = 0; i < constructorParams.Length; i++)
        {
            var param = constructorParams[i];
            var paramType = param.ParameterType;

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
                        $"Cannot create {pageType.Name}: parameter '{param.Name}' of type {paramType.Name} " +
                        "was not provided and cannot be resolved from the service container.");
                }
            }
        }

        return (T)Activator.CreateInstance(pageType, args)!;
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

        // Get all constructors for the page type
        var constructors = pageType.GetConstructors();

        // Find the best matching constructor
        var bestConstructor = FindBestConstructor(constructors, parameters);

        if (bestConstructor == null)
        {
            throw new InvalidOperationException($"No suitable constructor found for {pageType.Name} with the provided parameters.");
        }

        // Get constructor parameters
        var constructorParams = bestConstructor.GetParameters();
        var args = new object[constructorParams.Length];

        // Track which provided parameters we've used
        var usedParameterIndices = new HashSet<int>();

        // Fill constructor arguments
        for (var i = 0; i < constructorParams.Length; i++)
        {
            var param = constructorParams[i];
            var paramType = param.ParameterType;

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
                        $"Cannot create {pageType.Name}: parameter '{param.Name}' of type {paramType.Name} " +
                        "was not provided and cannot be resolved from the service container.");
                }
            }
        }

        return (IPage)Activator.CreateInstance(pageType, args)!;
    }

    private static ConstructorInfo? FindBestConstructor(ConstructorInfo[] constructors, object[] parameters)
    {
        // Prefer constructors that can use all provided parameters
        var parameterTypes = parameters.Select(p => p.GetType()).ToArray();

        return constructors
               .Where(c => CanConstructorUseParameters(c, parameterTypes))
               .OrderByDescending(c => c.GetParameters().Length)
               .FirstOrDefault();
    }

    private static bool CanConstructorUseParameters(ConstructorInfo constructor, Type[] parameterTypes)
    {
        var constructorParams = constructor.GetParameters();
        var usedTypes = new HashSet<Type>();

        foreach (var paramType in parameterTypes)
        {
            var matchingParam = constructorParams.FirstOrDefault(p =>
                IsAssignableFrom(p.ParameterType, paramType) && !usedTypes.Contains(p.ParameterType));

            if (matchingParam != null)
            {
                usedTypes.Add(matchingParam.ParameterType);
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