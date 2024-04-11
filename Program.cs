using System.Linq;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Storage;
using Windows.UI.Xaml;

namespace cycloid;

public static class Program
{
    private static void Main(string[] args)
    {
        IActivatedEventArgs activatedArgs = AppInstance.GetActivatedEventArgs();

        if (activatedArgs is FileActivatedEventArgs fileArgs)
        {
            IStorageFile file = (IStorageFile)fileArgs.Files.FirstOrDefault();
            if (file != null)
            {
                if (RegisterForFile(file, out AppInstance instance))
                {
                    Application.Start((p) => new App());
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
                Application.Start((p) => new App());
            }
        }
    }

    public static bool RegisterForFile(IStorageFile file, out AppInstance instance)
    {
        return (instance = AppInstance.FindOrRegisterInstanceForKey(file.Path.ToLowerInvariant())).IsCurrentInstance;
    }
}
