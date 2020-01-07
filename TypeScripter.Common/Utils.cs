using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace TypeScripter.Common {
	public static class Utils {
		public static string Camelize(this string value) {
			char[] chars = value.ToCharArray();
			chars[0] = char.ToLower(chars[0]);
			return new string(chars);
		}

		public static bool WriteIfChanged(string text, string path) {
			if(!File.Exists(path) || !string.Equals(text, File.ReadAllText(path))) {
				File.WriteAllText(path, text);
				return true;
			}
			return false;
		}
		
		public static TypeDetails ToTypeScriptType(this Type t, PropertyInfo p = null) {
			if (IsModelType(t) || t.IsEnum) {
				return new TypeDetails(t.Name);
			}
			if (t == typeof(bool)) {
				return new TypeDetails("boolean");
			}
			if (   t == typeof(byte)
				|| t == typeof(sbyte)
				|| t == typeof(ushort)
				|| t == typeof(short)
				|| t == typeof(uint)
				|| t == typeof(int)
				|| t == typeof(ulong)
				|| t == typeof(long)
				|| t == typeof(float)
				|| t == typeof(double)
				|| t == typeof(decimal)) {
				return new TypeDetails("number");
			}
			if (t == typeof(string) || t == typeof(char) || t == typeof(Guid)) {
				return new TypeDetails("string");
			}
			if (t.IsArray) {
				return new TypeDetails(ToTypeScriptType(t.GetElementType(), p) + "[]", " = []");
			}
			if (t.IsGenericType && IsGenericEnumerable(t)) {
				return new TypeDetails(ToTypeScriptType(t.GetGenericArguments()[0], p) + "[]", " = []");
			}
			if (t.Name == "Nullable`1") {
				return ToTypeScriptType(t.GetGenericArguments()[0], p);
			}
			if (t == typeof(DateTime))
			{
				var utc = p?.GetCustomAttributes().Any(x => x.GetType().Name == "TypeScripterUtcDateAttribute");
				return new TypeDetails("moment.Moment"){UtcDate = utc ?? false};
			}
			if (t.IsGenericType && t.GetGenericArguments().Length == 1)
			{
				return ToTypeScriptType(t.GetGenericArguments()[0], p);
			}

			return new TypeDetails("any");
		}

		private static bool IsGenericEnumerable(Type t)
		{
			if (!t.IsGenericType) {
				return false;
			}
			var typeDef = t.GetGenericTypeDefinition();
			return typeDef != typeof(Dictionary<,>)
				&& typeDef != typeof(IDictionary<,>)
				&& (
					typeDef == typeof(IEnumerable<>) 
					|| t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
				);
		}

		public static bool IsModelType(this Type t) {
			if(!t.IsClass || t.Namespace == null || t == typeof(string)) {
				return false;
			}

			bool isModel = t.FullName != null && !t.FullName.StartsWith("System.") && !t.FullName.StartsWith("Microsoft.");
			return isModel;
		}

        private static readonly HashSet<string> NonModelTypes = 
            new HashSet<string> {"boolean", "number", "string", "moment.Moment", "any"};

        public static bool IsOrContainsModelType(this Type type)
	    {
            // Note: When Enum support is implemented, this code will need to
            //       be updated to handle enums properly (right now enums act
            //       like objects)
	        var typescriptType = ToTypeScriptType(type).Name.Replace("[]", "");
	        return !NonModelTypes.Contains(typescriptType);
	    }

	    public static Type GetPropertyType(this PropertyInfo pi) {
			if(pi.PropertyType.IsGenericType) {
				return pi.PropertyType.GetGenericArguments()[0];
			}
			return pi.PropertyType;
		}

		public static bool IsNotAbstractModelType(this Type t) {
			return !t.IsAbstract && IsModelType(t);
		}

		public static ClassMemberInfo[] GetAllPropertiesInType(this Type t) {
			return t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
					.Select(p => new ClassMemberInfo { Name = p.Name, Type = ToTypeScriptType(p.PropertyType, p) })
					.Distinct()
					.OrderBy(p => p.Name)
					.ToArray();
		}

		public static ClassMemberInfo[] GetDeclaredPropertiesInType(this Type t) {
			return t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
					.Select(p => new ClassMemberInfo { Name = p.Name, Type = ToTypeScriptType(p.PropertyType,p) })
					.Distinct()
					.OrderBy(p => p.Name)
					.ToArray();
		}

		public static ClassMemberInfo[] GetStaticPropertiesForType(this Type t)
		{
			var result = t.GetDeclaredConstantsInType();
			var emitStaticReadonlyMembers = t.GetInterface("ITypeScripterEmitStaticReadonlyMembers") != null;
			if (emitStaticReadonlyMembers)
			{
				result = result.Concat(t.GetStaticReadonlyPropertiesInType()).ToArray();
			}
			return result;
		}

		private static ClassMemberInfo[] GetDeclaredConstantsInType(this Type t) {
			Type[] allowedTypes = { typeof(int), typeof(string) };
			return t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
				.Where(x => x.IsLiteral && !x.IsInitOnly && allowedTypes.Contains(x.FieldType))
				.Select(p => new ClassMemberInfo {
					Name = p.Name, Type = ToTypeScriptType(p.FieldType), Value = p.GetRawConstantValue().ToString()
				}).Distinct()
				.OrderBy(p => p.Name)
				.ToArray();
		}

		private static ClassMemberInfo[] GetStaticReadonlyPropertiesInType(this Type t)
		{
			return t
				.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
				.Where(x => x.IsInitOnly)
				.Select(x => new ClassMemberInfo {
					Name = x.Name, 
					Type = ToTypeScriptType(x.FieldType), 
					Value = x.GetValue(null).ToString()
				})
				.Distinct()
				.OrderBy(x => x.Name)
				.ToArray();
		}

		public static IEnumerable<string> FindChildModelTypeNames(this Type parentType) {
			return parentType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
					.Select(p => p.GetPropertyType())
					.Where(t => t.IsModelType() || t.IsEnum)
					.Where(x => x != parentType)
					.Distinct()
					.OrderBy(p => p.Name)
					.Select(p => p.Name)
					.ToArray();
		}

		public static ClassMemberInfo[] GetItterableModelPropertiesInType(this Type t) {
			return t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(x =>
					x.PropertyType.IsGenericType
					&& typeof(IEnumerable<object>).IsAssignableFrom(x.PropertyType)
					&& x.PropertyType.GetGenericArguments()[0].IsNotAbstractModelType())
				.Select(p => new ClassMemberInfo() { Name = p.Name, Type = p.PropertyType.GetGenericArguments()[0].ToTypeScriptType(p:p) })
				.Distinct()
				.OrderBy(p => p.Name)
				.ToArray();

		}

		public static ClassMemberInfo[] GetModelPropertiesInType(this Type t) {
			return t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
					.Where(x => x.PropertyType.IsNotAbstractModelType())
					.Select(p => new ClassMemberInfo { Name = p.Name, Type = p.PropertyType.ToTypeScriptType(p:p) })
					.Distinct()
					.OrderBy(p => p.Name)
					.ToArray();
		}
	}
}