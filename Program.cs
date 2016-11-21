using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace TypeScripter
{
	class Program {
		private static string _basePath;

		private static Options options;

		static int Main(string[] args)
		{
			if (args.Length != 1 && args.Length != 2)
			{
				Console.WriteLine("Usage: typescripter.exe <Options File Path>");
				Console.WriteLine("       typescripter.exe <dll source path> <model target path>");
				Console.WriteLine("Example: typescripter.exe \"Options.json\"");
				Console.WriteLine("         typescripter.exe \"c:\\Source\\Project\\bin\\debug\" \"c:\\Source\\Project\\src\\app\\models\\generated\"");
				Console.WriteLine("------------------------------------");
				for (var i = 0; i < args.Length; i++)
				{
					Console.WriteLine("[{0}] = {1}", i, args[i]);
				}
				return 1;
			}

			if (args.Length == 1)
			{
				if (File.Exists(args[0]))
				{
					try
					{
						options = FileHandler.ReadJson<Options>(args[0]);
						if (string.IsNullOrEmpty(options.Source))
						{
							throw new Exception("Source is null or empty in options.");
						}
						if (string.IsNullOrEmpty(options.Destination))
						{
							throw new Exception("Destination is null or empty in options.");
						}
						if (options.Files == null || options.Files.Length <= 0)
						{
							options.Files = new[] {"*.client.dll"};
						}
						if (options.ControllerBaseClassNames == null || options.ControllerBaseClassNames.Length <= 0)
						{
							options.ControllerBaseClassNames = new[] {"ApiController"};
						}
					}
					catch (Exception e)
					{
						Console.WriteLine(e.Message);
						return 2;
					}
				}
				else
				{
					Console.WriteLine("No options file found.");
					return 3;
				}
			}
			else if (args.Length == 2)
			{
				options = new Options
				{
					Source = args[0],
					Destination = args[1],
					Files = new[] {"*.client.dll"},
					ControllerBaseClassNames = new[] {"ApiController"}
				};
			}

			Stopwatch sw = Stopwatch.StartNew();
			AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly; // Need to look in the base path for referenced .dll files instead of the startup path

			_basePath = AbsolutePath(options.Source);
			var targetPath = AbsolutePath(options.Destination);
			Console.Write("Scanning for DTO objects in {0}...  ", _basePath);

			// Get dll files from options file list.
			var dlls = options.Files.SelectMany(f => Directory.GetFiles(_basePath, f));

			// Load the assemblies so we can reflect on them
			var assemblies = dlls.Select(Load).Where(a => a != null);

			// Find all Types that inherit from ApiController
			var apiControllers = assemblies.SelectMany(GetApiControllers);

			// Get all types that are returned from an API call or are in the parameter list
			var allModels = new HashSet<Type>(apiControllers.SelectMany(GetModelsFromController));

			// Now recursively search the top level models for child models
			foreach (var model in allModels.ToArray()) {
				RecursivelySearchModels(model, allModels);
			}
			Console.WriteLine("Found {0}", allModels.Count);

			if (!Directory.Exists(targetPath)) {
				Directory.CreateDirectory(targetPath);
			}

			bool somethingChanged = false;

			// Create and write new models
			Console.Write("Generating TypeScript classes in {0}...  ", targetPath);
			foreach (var m in allModels.OrderBy(m => m.Name)) {
				string filePath = Path.Combine(targetPath, m.Name + ".ts");
				string model = CreateModelString(m);
				if (!File.Exists(filePath) || !string.Equals(model, File.ReadAllText(filePath))) {
					File.WriteAllText(filePath, model);
					somethingChanged = true;
				}
			}

			if (somethingChanged) {
				// Make sure output directory does not contain any non-generated files that might cause problems
				var filesToDelete = Directory
					.GetFiles(targetPath)
					.Select(Path.GetFileName)
					.Except(allModels.Select(m => m.Name + ".ts"), StringComparer.OrdinalIgnoreCase);

				foreach (var file in filesToDelete) {
					File.Delete(Path.Combine(targetPath, file));
				}
			}

			string contents = string.Join(Environment.NewLine, allModels.OrderBy(m => m.Name).Select(m => string.Format("export * from './{0}';", m.Name)));
			string indexPath = Path.Combine(targetPath, "index.ts");

			if (!File.Exists(indexPath) || !string.Equals(contents, File.ReadAllText(indexPath)))
			{
				File.WriteAllText(indexPath, contents);
			}

			Console.WriteLine("Done!  Created {0} TypeScript models in {1:N3}s", allModels.Count, sw.Elapsed.TotalSeconds);

			return 0;
		}

		private static string AbsolutePath(string relativePath) {
			return Path.IsPathRooted(relativePath) ? relativePath : Path.Combine(Environment.CurrentDirectory, relativePath);
		}

		private static Assembly ResolveAssembly(object sender, ResolveEventArgs args) {
			try {
				string file = Path.Combine(_basePath, args.Name.Substring(0, args.Name.IndexOf(",", StringComparison.Ordinal)) + ".dll");
				return Assembly.LoadFile(file);
			}
			catch {
				Console.WriteLine(args.Name);
				return null;
			}
		}

		static Assembly Load(string path) {
			try {
				return Assembly.LoadFile(path);
			}
			catch {
				return null;
			}
		}

		static IEnumerable<Type> GetApiControllers(Assembly a) {
			return TryGetApiControllerTypes(a).Where(t => t.BaseType != null && options.ControllerBaseClassNames.Contains(t.BaseType.Name));
		}

		static IEnumerable<Type> GetModelsFromController(Type controllerType) {
			var methods = controllerType.GetMethods();
			var models = methods.SelectMany(GetModelsFromMethod);
			return models;
		}

		private static void RecursivelySearchModels(Type model, HashSet<Type> visitedModels) {
			var props = model
				.GetProperties()
				.Select(GetPropertyType).SelectMany(GetModelTypes).Where(t => !visitedModels.Contains(t) && IsModelType(t));
			foreach (var p in props) {
				visitedModels.Add(p);
				RecursivelySearchModels(p, visitedModels);
			}
		}

		private static Type GetPropertyType(PropertyInfo pi) {
			if (pi.PropertyType.IsGenericType) {
				return pi.PropertyType.GetGenericArguments()[0];
			}
			return pi.PropertyType;
		}

		private static string CreateModelString(Type t) {
			var declaration = (t.IsAbstract ? "export abstract class " : "export class ") + t.Name;
			var baseClass = t.BaseType != null && IsModelType(t.BaseType) ? t.BaseType.Name : "";

			var allProps = GetAllPropertiesInType(t);
			StringBuilder sb = new StringBuilder();

			if (allProps.Any(p => p.Type == "moment.Moment" || p.Type == "moment.Moment?")) {
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
			var declaredProps = GetDeclaredPropertiesInType(t);
			foreach (var prop in declaredProps) {
				sb.AppendLine(string.Format("\tpublic {0}: {1};", prop.Name, prop.Type));
			}

			if (!t.IsAbstract) {
				sb.AppendLine();
				sb.AppendLine("\tpublic constructor(");
				sb.AppendLine("\t\tfields?: {");
				sb.AppendLine(string.Join(",\n", allProps.Select(prop => string.Format("\t\t\t{0}?: {1}", prop.Name, prop.Type))));
				sb.AppendLine("\t\t}) {");
				sb.AppendLine();

				if (!string.IsNullOrWhiteSpace(baseClass)) {
					sb.AppendLine("\t\tsuper();");
				}

				sb.AppendLine("\t\tif (fields) {");

				var modelProps = GetModelPropertiesInType(t);
				sb.AppendLine(string.Join("\n", modelProps.Select(prop => string.Format("\t\t\tif (fields.{0}) {{ fields.{0} = new {1}(fields.{0}); }}", prop.Name, prop.Type))));
				sb.AppendLine(string.Join("\n", allProps.Where(x => x.Type == "moment.Moment").Select(prop => string.Format("\t\t\tif (fields.{0}) {{ fields.{0} = moment(fields.{0}); }}", prop.Name))));

				sb.AppendLine("\t\t\tObject.assign(this, fields);");
				sb.AppendLine("\t\t}");
				sb.AppendLine("\t}");
			}

			sb.AppendLine("}");

			return sb.ToString();
		}

		private static NameAndType[] GetDeclaredPropertiesInType(Type t) {
			return t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
					.Select(p => new NameAndType {Name = p.Name, Type = GetTypescriptType(p.PropertyType)})
					.Distinct()
					.OrderBy(p => p.Name)
					.ToArray();
		}

		private static NameAndType[] GetAllPropertiesInType(Type t) {
			return t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
					.Select(p => new NameAndType {Name = p.Name, Type = GetTypescriptType(p.PropertyType)})
					.Distinct()
					.OrderBy(p => p.Name)
					.ToArray();
		}

		private static NameAndType[] GetModelPropertiesInType(Type t) {
			return t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
					.Where(x => IsModelTypeNoAbstract(x.PropertyType))
					.Select(p => new NameAndType { Name = p.Name, Type = GetTypescriptType(p.PropertyType)})
					.Distinct()
					.OrderBy(p => p.Name)
					.ToArray();
		}

		private static string[] FindTypesToImport(Type parentType) {
			return parentType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
					.Select(GetPropertyType)
					.Where(IsModelType)
					.Where(x => x != parentType)
					.Distinct()
					.OrderBy(p => p.Name)
					.Select(p => p.Name)
					.ToArray();
		}

		static IEnumerable<Type> TryGetApiControllerTypes(Assembly a) {
			try {
				return a.GetTypes();
			}
			catch {
				return Enumerable.Empty<Type>();
			}
		}

		private static IEnumerable<Type> GetModelsFromMethod(MethodInfo arg) {
			return GetModelTypes(arg.ReturnType)
				.Union(arg.GetParameters()
						  .Select(p => p.ParameterType)
						  .SelectMany(GetModelTypes)
				);
		}

		private static IEnumerable<Type> GetModelTypes(Type t) {
			if (IsModelType(t)) {
				if (t.IsArray) {
					yield return t.GetElementType();
				}
				else {
					yield return t;
				}
			} else {
				if (t.IsGenericType) {
					foreach (var a in t.GetGenericArguments().Where(IsModelType).SelectMany(GetModelTypes)) {
						yield return a;
					}
				}
			}
			if (t.BaseType != null && IsModelType(t.BaseType)) {
				yield return t.BaseType;
			}
		}

		private static bool IsModelType(Type t) {
			if (!t.IsClass || t.Namespace == null || t == typeof(string)) {
				return false;
			}

			bool isModel = t.FullName != null && !t.FullName.StartsWith("System.");
			return isModel;
		}

		private static bool IsModelTypeNoAbstract(Type t) {
			return !t.IsAbstract && IsModelType(t);
		}

		private static string GetTypescriptType(Type t) {
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
				return GetTypescriptType(t.GetGenericArguments()[0]) + "[]";
			}
			if (t.Name == "Nullable`1") {
				return GetTypescriptType(t.GetGenericArguments()[0]);
			}
			if (t == typeof(DateTime)) {
				return "moment.Moment";
			}

			return "any";
		}

		private class NameAndType {
			public string Name { get; set; }
			public string Type { get;set; }
		}
	}
}
