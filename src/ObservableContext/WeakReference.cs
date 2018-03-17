using System;
using System.Runtime.Serialization;

namespace ObservableContext
{
    [Serializable]
    internal class WeakReference<T> : WeakReference
    {
        public WeakReference(T target)
            : base(target)
        {
        }

        public WeakReference(T target, bool trackResurrection)
            : base(target, trackResurrection)
        {
        }

        public WeakReference(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }

        public new T Target => (T)base.Target;
    }
}