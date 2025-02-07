using System.Diagnostics;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace cycloid;
#pragma warning restore IDE0130 // Namespace does not match folder structure

public static class TaskExtensions
{
    [StackTraceHidden]
    public static void FireAndForget(this Task task)
    {
        _ = ForgetAsync();

        [StackTraceHidden]
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