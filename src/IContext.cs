using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ObservableContext
{
    public interface IContext
    {
        bool TryGet<T>(string key, out T result);

        T Get<T>(string key);
    }

    public static class ContextExtensions
    {
        public static double TryGetDouble(this IContext context, string key, double defaultValue)
        {
            if (!context.TryGet<object>(key, out var result))
            {
                return defaultValue;
            }

            try
            {
                return Convert.ToDouble(result, CultureInfo.InvariantCulture);
            }
            catch
            {
                return defaultValue;
            }
        }

        public static IEnumerable<T> TryGetMany<T>(this IContext context, string key)
        {
            if (!context.TryGet<object>(key, out var result))
            {
                return null;
            }

            if (result is T t)
            {
                return new[] { t };
            }

            if (result is IEnumerable<T> enumerable)
            {
                return enumerable;
            }

            if (result is IEnumerable nonGeneric)
            {
                return nonGeneric.OfType<T>().ToList();
            }

            return null;
        }

        public static IEnumerable<T> GetMany<T>(this IContext context, string key)
        {
            var result = context.Get<object>(key);
            if (result is T t)
            {
                return new[] { t };
            }

            if (result is IEnumerable<T> enumerable)
            {
                return enumerable;
            }

            if (result is IEnumerable nonGeneric)
            {
                return nonGeneric.OfType<T>().ToList();
            }

            throw new InvalidOperationException($"Invalid type returned from a value. Expected 'IEnumerable<{typeof(T)}>'.");
        }

        public static void SetFunc<T>(this Context context, string key, Func<IContext, T> func)
        {
            context.Set(key, new FuncValue<T>(func), false);
        }

        public static void SetNonEnumerable<T>(this Context context, string key, T value)
        {
            context.Set(key, new LiteralValue<T>(value), false);
        }

        public static IAnonymousValue<T> SubscribeFunc<T>(this Context context, string key, Func<IContext, T> func)
        {
            return context.Subscribe(new FuncValue<T>(func));
        }
    }
}
