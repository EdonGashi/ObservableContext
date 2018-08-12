using System;

namespace ObservableContext
{
    public abstract class LazyValue<T> : ValueBase<T>, ILazyValue
    {
        private Lazy<T> lazy;

        protected LazyValue()
        {
            Invalidate();
        }

        object ILazyValue.GetValue() => lazy.Value;

        public T GetValue() => lazy.Value;

        public void Invalidate()
        {
            lazy = new Lazy<T>(Get);
        }

        public sealed override T Get(IContext context) => lazy.Value;

        protected abstract T Get();
    }
}