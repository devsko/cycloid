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
                    Run();
                }
                else
                {
                    instance.RedirectActivationTo();
                }
            }
        }
        else
        {
            if (AppInstance.RecommendedInstance is not null)
            {
                AppInstance.RecommendedInstance.RedirectActivationTo();
            }
            else
            {
                Run();
            }
        }
    }

    private static void Run()
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
