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
#pragma warning disable VSTHRD003 // Avoid awaiting foreign Tasks
                await task.ConfigureAwait(false);
#pragma warning restore VSTHRD003 // Avoid awaiting foreign Tasks
            }
            catch (Exception ex)
            {
                await App.Current.ShowExceptionAsync(ex);
            }
        }
    }
}