using System;
using Newtonsoft.Json.Linq;

namespace ObservableContext
{
    public abstract class ValueBase<T> : IValue<T>
    {
        public Type Type => typeof(T);

        object IValue.Get(IContext context) => Get(context);

        public abstract T Get(IContext context);

        public abstract JToken Tokenize();
    }
}
