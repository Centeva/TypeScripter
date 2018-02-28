using System;
using System.CodeDom;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace TypeScripter {
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
		
		public static TypeDetails ToTypeScriptType(this Type t) {
			if(IsModelType(t) || t.IsEnum) {
				return new TypeDetails(t.Name);
			}
			if(t == typeof(bool)) {
				return new TypeDetails("boolean");
			}
			if(t == typeof(byte)
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
			if(t == typeof(string) || t == typeof(char)) {
				return new TypeDetails("string");
			}
			if(t.Name == "List`1" || (t.IsGenericType && typeof(IEnumerable<object>).IsAssignableFrom(t))) {
				return new TypeDetails(ToTypeScriptType(t.GetGenericArguments()[0]) + "[]", " = []");
			}
			if(t.Name == "Nullable`1") {
				return ToTypeScriptType(t.GetGenericArguments()[0]);
			}
			if(t == typeof(DateTime)) {
				return new TypeDetails("moment.Moment");
			}

			return new TypeDetails("any");
		}

		public static bool IsModelType(this Type t) {
			if(!t.IsClass || t.Namespace == null || t == typeof(string)) {
				return false;
			}

			bool isModel = t.FullName != null && !t.FullName.StartsWith("System.") && !t.FullName.StartsWith("Microsoft.");
			return isModel;
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
					.Select(p => new ClassMemberInfo { Name = p.Name, Type = ToTypeScriptType(p.PropertyType) })
					.Distinct()
					.OrderBy(p => p.Name)
					.ToArray();
		}

		public static ClassMemberInfo[] GetDeclaredPropertiesInType(this Type t) {
			return t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
					.Select(p => new ClassMemberInfo { Name = p.Name, Type = ToTypeScriptType(p.PropertyType) })
					.Distinct()
					.OrderBy(p => p.Name)
					.ToArray();
		}

		public static ClassMemberInfo[] GetDeclaredConstantsInType(this Type t) {
			Type[] allowedTypes = { typeof(int), typeof(string) };
			return t.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
				.Where(x => x.IsLiteral && !x.IsInitOnly && allowedTypes.Contains(x.FieldType))
				.Select(p => new ClassMemberInfo {
					Name = p.Name, Type = ToTypeScriptType(p.FieldType), Value = p.GetRawConstantValue().ToString()
				}).Distinct()
				.OrderBy(p => p.Name)
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
				.Select(p => new ClassMemberInfo() { Name = p.Name, Type = p.PropertyType.GetGenericArguments()[0].ToTypeScriptType() })
				.Distinct()
				.OrderBy(p => p.Name)
				.ToArray();

		}

		public static ClassMemberInfo[] GetModelPropertiesInType(this Type t) {
			return t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
					.Where(x => x.PropertyType.IsNotAbstractModelType())
					.Select(p => new ClassMemberInfo { Name = p.Name, Type = p.PropertyType.ToTypeScriptType() })
					.Distinct()
					.OrderBy(p => p.Name)
					.ToArray();
		}
	}
}