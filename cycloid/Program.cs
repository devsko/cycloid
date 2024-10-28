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

        if (activatedArgs is FileActivatedEventArgs fileArgs)
        {
            if (fileArgs.Files is [IStorageFile file, ..])
            {
                if (RegisterForFile(file, out AppInstance instance))
                {
                    StartApp();
                }
                else
                {
                    instance.RedirectActivationTo();
                }
            }
        }
        else
        {
            if (AppInstance.RecommendedInstance != null)
            {
                AppInstance.RecommendedInstance.RedirectActivationTo();
            }
            else
            {
                StartApp();
            }
        }
    }

    private static void StartApp()
    {
        Application.Start(p =>
        {
            SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            _ = new App();
        });
    }

    public static bool RegisterForFile(IStorageFile file, out AppInstance instance)
    {
        return (instance = AppInstance.FindOrRegisterInstanceForKey(file.Path.ToLowerInvariant())).IsCurrentInstance;
    }
}
