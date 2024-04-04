using Microsoft.Extensions.DependencyInjection;
using PZEditAvaloniaUI.ViewModels;

namespace PZEditAvaloniaUI;

public static class ServiceCollectionExtensions
{
    public static void AddCommonServices(this IServiceCollection collection)
    {
        collection.AddTransient<MainWindowViewModel>();
    }
}