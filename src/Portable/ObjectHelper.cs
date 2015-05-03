namespace System
{
    using System.Reflection;
    using System.Linq;
    using System.Collections.Generic;

    static class ObjectHelper<T> where T : new()
    {
        private static readonly PropertyInfo[] mergeProperties = (
            from pi in typeof(T).GetTypeInfo().DeclaredProperties
            where pi.GetMethod != null && pi.GetMethod.IsPublic && pi.SetMethod != null && pi.SetMethod.IsPublic
            select pi).ToArray();

        private static readonly FieldInfo[] mergeFields = (
            from fi in typeof(T).GetTypeInfo().DeclaredFields where fi.IsPublic select fi).ToArray();

        public static T Merge(T target, T change)
        {
            if (Equals(target, change)) return target;

            foreach (var pi in mergeProperties)
            {
                pi.SetMethod.Invoke(target, new[] { pi.GetMethod.Invoke(change, null) });
            }

            foreach (var pi in mergeFields)
            {
                pi.SetValue(target, pi.GetValue(change));
            }

            return target;
        }

        public static T Clone(T target)
        {
            return Merge(new T(), target);
        }
    }
}
