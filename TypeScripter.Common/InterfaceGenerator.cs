using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TypeScripter.Common
{
	internal class InterfaceGenerator
	{
		public static List<string> Generate(string targetPath, HashSet<Type> allModels, Options options)
		{
			var combineImports = options.CombineImports ?? false;
			if (!Directory.Exists(targetPath))
			{
				Directory.CreateDirectory(targetPath);
			}

			// Create and write new models
			foreach (var m in allModels.OrderBy(m => m.Name))
			{
				Utils.WriteIfChanged(CreateModelString(m, combineImports), Path.Combine(targetPath, m.Name + ".ts"));
			}

			Console.WriteLine("Created {0} TypeScript models.", allModels.Count);
			return allModels.Select(m => m.Name).ToList();
		}

		private static string CreateModelString(Type t, bool combineImports)
		{
			if (t.IsEnum)
			{
				return GenerateEnum(t);
			}
			else
			{
				return GenerateClass(t, combineImports);
			}
		}

		private static string GenerateEnum(Type t)
		{
			var enumValues = Enum.GetValues(t);

			var sb = new StringBuilder();
			sb.AppendFormat("export enum {0} {{", t.Name);

			foreach (var value in enumValues)
			{
				sb.AppendLine();
				sb.AppendFormat("\t{0} = {1},", value, (int)value);
			}
			sb.AppendLine(); // new line after last enum value
			sb.AppendLine("}");

			return sb.ToString();
		}

		private static string GenerateClass(Type t, bool combineImports)
		{
			var sb = new StringBuilder();
			var allProps = t.GetAllPropertiesInType();
			var baseClass = t.BaseType != null && t.BaseType.IsModelType() ? t.BaseType.Name : "";
			var importTypes = new HashSet<string>();

			// Write import statements
			if (allProps.Any(p => p.Type.Name == "moment.Moment" || p.Type.Name == "moment.Moment?"))
			{
				sb.AppendLine("import * as moment from 'moment';");
			}
			if (!string.IsNullOrWhiteSpace(baseClass))
			{
				importTypes.Add(baseClass);
			}
			importTypes.UnionWith(t.FindChildModelTypeNames());

			// If the option is configured, combine the model imports to use the generated index.
			if (combineImports)
			{
				sb.AppendLine("import {");
				foreach (var import in importTypes)
				{
					sb.AppendLine(string.Format("\t{0},", import));
				}
				sb.AppendLine("} from './';\n");
			}
			else
			{
				foreach (var import in importTypes)
				{
					sb.AppendLine($"import {{ {import}, {import}_fromJsonObj }} from './{import}';");
				}
				sb.AppendLine();
			}

			// Write declaration
			sb.Append("export interface " + t.Name);
			sb.AppendLine(" {");

			// Write declared constants
			//var constants = t.GetDeclaredConstantsInType();
			//foreach (var prop in constants)
			//{
			//	sb.AppendLine(string.Format("\tpublic static readonly {0}: {1} = {2};", prop.Name, prop.Type.Name, prop.Value));
			//}
			//if (constants.Any())
			//{
			//	sb.AppendLine();
			//}

			// Write declared properties
			var props = t.BaseType != null && !t.BaseType.IsModelType() ? t.GetAllPropertiesInType() : t.GetDeclaredPropertiesInType();
			foreach (var prop in props)
			{
				sb.AppendLine(string.Format("\t{0}: {1};", prop.Name, prop.Type.Name, prop.Type.Initializer));
			}

			sb.AppendLine("}");

			// Write constructor
			if (!t.IsAbstract)
			{
				sb.AppendLine($"export function {t.Name}_fromJsonObj(obj:any): {t.Name} {{");
				if (!string.IsNullOrWhiteSpace(baseClass))
				{
					sb.AppendLine($"\tobj = {baseClass}_fromJsonObj(obj);");
				}

				var modelProps = t.GetModelPropertiesInType();
				var modelItterables = t.GetItterableModelPropertiesInType();

				sb.AppendLine($"\treturn {{");


				sb.AppendLine(new List<IEnumerable<string>>{
						new []{"\t\t...obj"},
						GetNonUtcDateSegments(allProps),
						GetUtcDateSegments(allProps),
						GetModelSegments(modelProps),
						GetModelListSegments(modelItterables)
					}.SelectMany(x => x)
					.Aggregate(ObjPropLinesAgg));

				sb.AppendLine("\t};");
				sb.AppendLine("}");
			}

			//return sb.AppendLine("}").ToString();
			return sb.ToString();
		}

		private static string ObjPropLinesAgg (string a, string b) { 
			return $"{a},\n{b}";
		}

		private static IEnumerable<string> GetUtcDateSegments(ClassMemberInfo[] allProps)
		{
			
			return allProps
				.Where(x => x.Type.Name == "moment.Moment" && x.Type.UtcDate)
				.Select(prop => string.Format("\t\t\tif (fields.{0}) {{ fields.{0} = moment.utc(fields.{0}); }}", prop.Name));
		}

		private static IEnumerable<string> GetNonUtcDateSegments(ClassMemberInfo[] allProps)
		{
			return allProps
				.Where(x => x.Type.Name == "moment.Moment" && !x.Type.UtcDate)
				.Select(prop => string.Format("\t\t\tif (fields.{0}) {{ fields.{0} = moment(fields.{0}); }}", prop.Name));
		}

		private static IEnumerable<string> GetModelSegments(ClassMemberInfo[] modelProps)
		{
			return modelProps
				.Select(prop => $"\t\t{prop.Name}: obj.{prop.Name} && {prop.Type}_fromJsonObj(obj.{prop.Name})");
		}

		private static IEnumerable<string> GetModelListSegments(ClassMemberInfo[] modelItterables)
		{
			return modelItterables
				.Select(prop => $"\t\t{prop.Name}: obj.{prop.Name} && obj.{prop.Name}.map((x:any) => {prop.Type}_fromJsonObj(x))");
		}
	}
}