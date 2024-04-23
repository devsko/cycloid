namespace cycloid;

// WORKAROUND the problem that UWP binding is not wworking reliably with null values.
// https://github.com/microsoft/microsoft-ui-xaml/issues/2166
public interface ICanBeInvalid<T> where T : struct, ICanBeInvalid<T>
{
    bool IsValid { get; }
    T Invalid { get; }
}
