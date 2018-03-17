namespace ObservableContext
{
    public interface IValue<out T>
    {
        T Get(IContext context);
    }
}