using System.Linq;
using System.Threading;
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
            IStorageFile file = (IStorageFile)fileArgs.Files.FirstOrDefault();
            if (file != null)
            {
                if (RegisterForFile(file, out AppInstance instance))
                {
                    Application.Start(_ =>
                    {
                        SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
                        new App();
                    });
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
                Application.Start(_ =>
                {
                    SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
                    new App();
                });
            }
        }
    }

    public static bool RegisterForFile(IStorageFile file, out AppInstance instance)
    {
        return (instance = AppInstance.FindOrRegisterInstanceForKey(file.Path.ToLowerInvariant())).IsCurrentInstance;
    }
}
