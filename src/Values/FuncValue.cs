using System;
using Newtonsoft.Json.Linq;

namespace ObservableContext
{
    public class FuncValue<T> : ValueBase<T>
    {
        private readonly Func<IContext, T> func;

        public FuncValue(Func<IContext, T> func)
        {
            this.func = func;
        }

        public override T Get(IContext context)
        {
            return func(context);
        }

        public override JToken Tokenize()
        {
            throw new NotSupportedException("Tokenizing a FuncValue is not supported.");
        }
    }
}
