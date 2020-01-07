using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TypeScripter.Common.Generators {
	public static class EntityGenerator {
		public static List<string> Generate(string targetPath, HashSet<Type> allModels, Options options)
		{
		  var combineImports = options.CombineImports ?? false;
			if(!Directory.Exists(targetPath)) {
				Directory.CreateDirectory(targetPath);
			}

			// Create and write new models
			foreach(var m in allModels.OrderBy(m => m.Name)) {
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
			if(allProps.Any(p => p.Type.Name == "moment.Moment" || p.Type.Name == "moment.Moment?")) {
				sb.AppendLine("import * as moment from 'moment';");
			}
			if(!string.IsNullOrWhiteSpace(baseClass))
			{
			    importTypes.Add(baseClass);
			}
		    importTypes.UnionWith(t.FindChildModelTypeNames());
            
			// If the option is configured, combine the model imports to use the generated index.
			if (combineImports) {
				sb.AppendLine("import {");
				foreach(var import in importTypes) {
					sb.AppendLine(string.Format("\t{0},", import));
				}
				sb.AppendLine("} from './';\n");
			} else {
				foreach (var import in importTypes) {
					sb.AppendLine(string.Format("import {{ {0} }} from './{0}';", import));
				}
				sb.AppendLine();
			}

			// Write declaration
			sb.Append((t.IsAbstract ? "export abstract class " : "export class ") + t.Name);
			if(!string.IsNullOrWhiteSpace(baseClass)) {
				sb.Append(" extends " + baseClass);
			}
			sb.AppendLine(" {");

			// Write static properties
			var constants = t.GetStaticPropertiesForType();
			foreach(var prop in constants) {
				sb.AppendLine(string.Format("\tpublic static readonly {0}: {1} = {2};", prop.Name, prop.Type.Name, prop.Value));
			}
			if(constants.Any()) {
				sb.AppendLine();
			}

			// Write declared properties
			var props = t.BaseType != null && !t.BaseType.IsModelType() ? t.GetAllPropertiesInType() : t.GetDeclaredPropertiesInType();
			foreach (var prop in props) {
				sb.AppendLine(string.Format("\tpublic {0}: {1} {2};", prop.Name, prop.Type.Name, prop.Type.Initializer));
			}

			// Write constructor
			if(!t.IsAbstract) {
				sb.AppendLine();
				sb.AppendLine("\tpublic constructor(");
				sb.AppendLine(string.Format("\t\tfields?: Partial<{0}>) {{", t.Name));
				sb.AppendLine();

				if(!string.IsNullOrWhiteSpace(baseClass)) {
					sb.AppendLine("\t\tsuper();");
				}

				sb.AppendLine("\t\tif (fields) {");

				var modelProps = t.GetModelPropertiesInType();
				var modelItterables = t.GetItterableModelPropertiesInType();
				sb.AppendLine(string.Join("\n", modelProps.Select(prop => string.Format("\t\t\tif (fields.{0}) {{ fields.{0} = new {1}(fields.{0}); }}", prop.Name, prop.Type))));
				sb.AppendLine(string.Join("\n", allProps.Where(x => x.Type.Name == "moment.Moment" && !x.Type.UtcDate).Select(prop => string.Format("\t\t\tif (fields.{0}) {{ fields.{0} = moment(fields.{0}); }}", prop.Name))));
				sb.AppendLine(string.Join("\n", allProps.Where(x => x.Type.Name == "moment.Moment" && x.Type.UtcDate).Select(prop => string.Format("\t\t\tif (fields.{0}) {{ fields.{0} = moment.utc(fields.{0}); }}", prop.Name))));
				sb.AppendLine(string.Join("\n", modelItterables.Select(prop => string.Format("\t\t\tif (fields.{0}) {{ fields.{0} = fields.{0}.map(x => new {1}(x)); }}", prop.Name, prop.Type))));

				sb.AppendLine("\t\t\tObject.assign(this, fields);");
				sb.AppendLine("\t\t}");
				sb.AppendLine("\t}");
			}

			return sb.AppendLine("}").ToString();
		}
	}
}