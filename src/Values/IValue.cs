using System;
using Newtonsoft.Json.Linq;

namespace ObservableContext
{
    public interface ITokenizable
    {
        JToken Tokenize();
    }

    public interface IValue : ITokenizable
    {
        Type Type { get; }

        object Get(IContext context);
    }

    public interface IValue<out T> : IValue
    {
        new T Get(IContext context);
    }

    public static class ValueExtensions
    {
        public static IValue<T> Coerce<T>(this IValue value)
        {
            if (value is IValue<T> tvalue)
            {
                return tvalue;
            }

            return new CoercedValue<T>(value);
        }
    }
}