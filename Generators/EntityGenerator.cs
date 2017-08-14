﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace TypeScripter.Generators
{
	public static class EntityGenerator
	{
		public static List<string> Generate(string targetPath, HashSet<Type> allModels)
		{
			if (!Directory.Exists(targetPath)) {
				Directory.CreateDirectory(targetPath);
			}

			// Create and write new models
			foreach (var m in allModels.OrderBy(m => m.Name)) {
				Utils.WriteIfChanged(CreateModelString(m), Path.Combine(targetPath, m.Name + ".ts"));
			}

			Console.WriteLine("Created {0} TypeScript models.", allModels.Count);
			return allModels.Select(m => m.Name).ToList();
		}

		private static string CreateModelString(Type t) {
			var declaration = (t.IsAbstract ? "export abstract class " : "export class ") + t.Name;
			var baseClass = t.BaseType != null && t.BaseType.IsModelType() ? t.BaseType.Name : "";

			var allProps = t.GetAllPropertiesInType();
			StringBuilder sb = new StringBuilder();

			if (allProps.Any(p => p.Type.Name == "moment.Moment" || p.Type.Name == "moment.Moment?")) {
				sb.AppendLine("import * as moment from 'moment';");
			}
			if (!string.IsNullOrWhiteSpace(baseClass)) {
				sb.AppendLine(string.Format("import {{ {0} }} from './{0}';", baseClass));
			}

			var importTypes = FindTypesToImport(t);
			foreach (var import in importTypes) {
				sb.AppendLine(string.Format("import {{ {0} }} from './{0}';", import));
			}

			sb.AppendLine();

			sb.Append(declaration);
			if (!string.IsNullOrWhiteSpace(baseClass)) {
				sb.Append(" extends " + baseClass);
			}

			sb.AppendLine(" {");
			var declaredProps = t.GetDeclaredPropertiesInType();
			foreach (var prop in declaredProps) {
				sb.AppendLine(string.Format("\tpublic {0}: {1} {2};", prop.Name, prop.Type.Name, prop.Type.Initializer));
			}

			if (!t.IsAbstract) {
				sb.AppendLine();
				sb.AppendLine("\tpublic constructor(");
				sb.AppendLine(string.Format("\t\tfields?: Partial<{0}>) {{", t.Name));
				sb.AppendLine();

				if (!string.IsNullOrWhiteSpace(baseClass)) {
					sb.AppendLine("\t\tsuper();");
				}

				sb.AppendLine("\t\tif (fields) {");

				var modelProps = GetModelPropertiesInType(t);
				var modelItterables = GetItterableModelPropertiesInType(t);
				sb.AppendLine(string.Join("\n", modelProps.Select(prop => string.Format("\t\t\tif (fields.{0}) {{ fields.{0} = new {1}(fields.{0}); }}", prop.Name, prop.Type))));
				sb.AppendLine(string.Join("\n", allProps.Where(x => x.Type.Name == "moment.Moment").Select(prop => string.Format("\t\t\tif (fields.{0}) {{ fields.{0} = moment(fields.{0}); }}", prop.Name))));
				sb.AppendLine(string.Join("\n", modelItterables.Select(prop => string.Format("\t\t\tif (fields.{0}) {{ fields.{0} = fields.{0}.map(x => new {1}(x)); }}", prop.Name, prop.Type))));

				sb.AppendLine("\t\t\tObject.assign(this, fields);");
				sb.AppendLine("\t\t}");
				sb.AppendLine("\t}");
			}

			sb.AppendLine("}");

			return sb.ToString();
		}

		private static string[] FindTypesToImport(Type parentType) {
			return parentType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
					.Select(p => p.GetPropertyType())
					.Where(t => t.IsModelType())
					.Where(x => x != parentType)
					.Distinct()
					.OrderBy(p => p.Name)
					.Select(p => p.Name)
					.ToArray();
		}

		private static NameAndType[] GetItterableModelPropertiesInType(Type t)
		{
			return t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(x => x.PropertyType.IsGenericType && typeof (IEnumerable<object>).IsAssignableFrom(x.PropertyType))
				.Select(p => new NameAndType() {Name = p.Name, Type = p.PropertyType.GetGenericArguments()[0].ToTypeScriptType() })
				.Distinct()
				.OrderBy(p => p.Name)
				.ToArray();

		}

		private static NameAndType[] GetModelPropertiesInType(Type t) {
			return t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
					.Where(x => x.PropertyType.IsModelTypeNoAbstract())
					.Select(p => new NameAndType { Name = p.Name, Type = p.PropertyType.ToTypeScriptType()})
					.Distinct()
					.OrderBy(p => p.Name)
					.ToArray();
		}
	}
}
