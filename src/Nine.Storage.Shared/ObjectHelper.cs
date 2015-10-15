namespace System
{
    using System.Collections.Generic;
    using System.Reflection;
    using System.Linq;

    static class ObjectHelper<T> where T : class, new()
    {
        private static readonly PropertyInfo[] mergeProperties = (
            from pi in GetAllProperties(typeof(T))
            where pi.GetMethod != null && pi.GetMethod.IsPublic && pi.SetMethod != null && pi.SetMethod.IsPublic
            select pi).ToArray();

        private static readonly FieldInfo[] mergeFields = (
            from fi in GetAllFields(typeof(T)) where fi.IsPublic select fi).ToArray();

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
            if (target == null) return null;
            return Merge(new T(), target);
        }

        private static IEnumerable<PropertyInfo> GetAllProperties(Type type)
        {
            while (type != null)
            {
                var ti = type.GetTypeInfo();
                foreach (var prop in ti.DeclaredProperties)
                {
                    yield return prop;
                }
                type = ti.BaseType;
            }
        }

        private static IEnumerable<FieldInfo> GetAllFields(Type type)
        {
            while (type != null)
            {
                var ti = type.GetTypeInfo();
                foreach (var field in ti.DeclaredFields)
                {
                    yield return field;
                }
                type = ti.BaseType;
            }
        }
    }
}
