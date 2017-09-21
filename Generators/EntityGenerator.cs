using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace TypeScripter.Generators {
	public static class EntityGenerator {
		public static List<string> Generate(string targetPath, HashSet<Type> allModels) {
			if(!Directory.Exists(targetPath)) {
				Directory.CreateDirectory(targetPath);
			}

			// Create and write new models
			foreach(var m in allModels.OrderBy(m => m.Name)) {
				Utils.WriteIfChanged(CreateModelString(m), Path.Combine(targetPath, m.Name + ".ts"));
			}

			Console.WriteLine("Created {0} TypeScript models.", allModels.Count);
			return allModels.Select(m => m.Name).ToList();
		}

		private static string CreateModelString(Type t) {
			var sb = new StringBuilder();
			var allProps = t.GetAllPropertiesInType();
			var baseClass = t.BaseType != null && t.BaseType.IsModelType() ? t.BaseType.Name : "";

			// Write import statements
			if(allProps.Any(p => p.Type.Name == "moment.Moment" || p.Type.Name == "moment.Moment?")) {
				sb.AppendLine("import * as moment from 'moment';");
			}
			if(!string.IsNullOrWhiteSpace(baseClass)) {
				sb.AppendLine(string.Format("import {{ {0} }} from './{0}';", baseClass));
			}
			var importTypes = t.FindChildModelTypeNames();
			foreach(var import in importTypes) {
				sb.AppendLine(string.Format("import {{ {0} }} from './{0}';", import));
			}
			sb.AppendLine();

			// Write declaration
			sb.Append((t.IsAbstract ? "export abstract class " : "export class ") + t.Name);
			if(!string.IsNullOrWhiteSpace(baseClass)) {
				sb.Append(" extends " + baseClass);
			}
			sb.AppendLine(" {");

			// Write declared constants
			var constants = t.GetDeclaredConstantsInType();
			foreach(var prop in constants) {
				sb.AppendLine(string.Format("\tpublic static readonly {0}: {1} = {2};", prop.Name, prop.Type.Name, prop.Value));
			}
			if(constants.Any()) {
				sb.AppendLine();
			}

			// Write declared properties
			var declaredProps = t.GetDeclaredPropertiesInType();
			foreach(var prop in declaredProps) {
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
				sb.AppendLine(string.Join("\n", allProps.Where(x => x.Type.Name == "moment.Moment").Select(prop => string.Format("\t\t\tif (fields.{0}) {{ fields.{0} = moment(fields.{0}); }}", prop.Name))));
				sb.AppendLine(string.Join("\n", modelItterables.Select(prop => string.Format("\t\t\tif (fields.{0}) {{ fields.{0} = fields.{0}.map(x => new {1}(x)); }}", prop.Name, prop.Type))));

				sb.AppendLine("\t\t\tObject.assign(this, fields);");
				sb.AppendLine("\t\t}");
				sb.AppendLine("\t}");
			}

			return sb.AppendLine("}").ToString();
		}
	}
}