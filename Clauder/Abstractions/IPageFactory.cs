namespace Clauder.Abstractions;

public interface IPageFactory
{
    T CreatePage<T>(params object[] parameters) where T : class, IPage;
    
    IPage CreatePage(Type pageType, params object[] parameters);
}