using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace TypeScripter
{
	public static class Utils
	{
		public static string ToTypeScriptType(this Type t)
		{
			if (IsModelType(t)) {
				return t.Name;
			}
			if (t == typeof(bool)) {
				return "boolean";
			}
			if (t == typeof(byte)
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
				return "number";
			}
			if (t == typeof(string) || t == typeof(char)) {
				return "string";
			}
			if (t.Name == "List`1" || (t.IsGenericType && typeof(IEnumerable<object>).IsAssignableFrom(t))) {
				return ToTypeScriptType(t.GetGenericArguments()[0]) + "[]";
			}
			if (t.Name == "Nullable`1") {
				return ToTypeScriptType(t.GetGenericArguments()[0]);
			}
			if (t == typeof(DateTime)) {
				return "moment.Moment";
			}

			return "any";
		}

		public static bool IsModelType(this Type t) {
			if (!t.IsClass || t.Namespace == null || t == typeof(string)) {
				return false;
			}

			bool isModel = t.FullName != null && !t.FullName.StartsWith("System.");
			return isModel;
		}

		public static bool IsModelTypeNoAbstract(this Type t) {
			return !t.IsAbstract && IsModelType(t);
		}

		public static NameAndType[] GetAllPropertiesInType(this Type t) {
			return t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
					.Select(p => new NameAndType {Name = p.Name, Type = ToTypeScriptType(p.PropertyType)})
					.Distinct()
					.OrderBy(p => p.Name)
					.ToArray();
		}

		public static NameAndType[] GetDeclaredPropertiesInType(this Type t) {
			return t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
					.Select(p => new NameAndType {Name = p.Name, Type = ToTypeScriptType(p.PropertyType)})
					.Distinct()
					.OrderBy(p => p.Name)
					.ToArray();
		}

		public static Type GetPropertyType(this PropertyInfo pi) {
			if (pi.PropertyType.IsGenericType) {
				return pi.PropertyType.GetGenericArguments()[0];
			}
			return pi.PropertyType;
		}

		public static string Camelize(this string value) {
			char[] chars = value.ToCharArray();
			chars[0] = char.ToLower(chars[0]);
			return new string(chars);
		}

		public static bool WriteIfChanged(string text, string path) {
			if (!File.Exists(path) || !string.Equals(text, File.ReadAllText(path))) {
				File.WriteAllText(path, text);
				return true;
			}
			return false;
		}
	}
}