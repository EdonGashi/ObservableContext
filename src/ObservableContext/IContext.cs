namespace ObservableContext
{
    public interface IContext
    {
        T Get<T>(string key);
    }
}