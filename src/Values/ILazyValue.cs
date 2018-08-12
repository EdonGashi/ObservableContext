namespace ObservableContext
{
    public interface ILazyValue : IValue
    {
        object GetValue();

        void Invalidate();
    }
}