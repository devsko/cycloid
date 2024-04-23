using System;
using System.Threading.Tasks;

namespace cycloid;

public static class TaskExtensions
{
    public static void FireAndForget(this Task task)
    {
        _ = ForgetAsync();

        async Task ForgetAsync()
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await App.Current.ShowExceptionAsync(ex);
            }
        }
    }
}