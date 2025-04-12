using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.System;
using Windows.UI.Xaml;

namespace cycloid;

public static class Program
{
    private static void Main()
    {
        IActivatedEventArgs activatedArgs = AppInstance.GetActivatedEventArgs();

        if (AppInstance.RecommendedInstance is not null)
        {
            AppInstance.RecommendedInstance.RedirectActivationTo();
        }
        else if (activatedArgs is FileActivatedEventArgs fileArgs && fileArgs.Files is [IStorageFile file, ..])
        {
            RedirectIfInstanceExists(file.Path);
        }
        else if (activatedArgs is LaunchActivatedEventArgs launchArgs && launchArgs.Arguments is { Length: > 0 } and not ['-', ..])
        {
            RedirectIfInstanceExists(launchArgs.Arguments);
        }

        Application.Start(p =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            _ = new App();
        });

        void RedirectIfInstanceExists(string filePath)
        {
            if (!RegisterForFile(filePath, out AppInstance instance))
            {
                instance.RedirectActivationTo();
            }
        }
    }

    public static bool RegisterForFile(string filePath, out AppInstance instance)
    {
        return (instance = AppInstance.FindOrRegisterInstanceForKey(filePath.ToLowerInvariant())).IsCurrentInstance;
    }
}
