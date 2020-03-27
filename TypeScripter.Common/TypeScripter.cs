using DocoptNet;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TypeScripter.Common.Generators;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Typescripter.Tests")]

namespace TypeScripter.Common
{
	public class TypeScripter
	{
		private static string _basePath;
		private static Options _options;

		// See http://docopt.org/
		// Formated with spaces purposely DO NOT REFORMAT
		private const string usage = @"Typescripter.

    Usage:
      Typescripter.exe <SETTINGSFILE>
      Typescripter.exe <SOURCE> <DESTINATION> [<APIPATH> [ --httpclient ] [ --combineimports ]]
                       [--files=<FILES> | --class=<CLASSNAMES>]...
      Typescripter.exe ( -h | --help )

    Options:
      --files=<FILES>         Comma seperated list of .dll files to generate models from. [ default: *.client.dll ]
      --class=<CLASSNAMES>    Comma seperated list of controller class names. [ default: ApiController ]
      --httpclient            Generated data service will use the new HttpClientModule for angular 4.
      --combineimports        Combines model imports to come from the generated index, rather than individual model files. [default: false]
      -h --help               Show this screen.

      <SETTINGSFILE>          Path to a json settings file
                                   example settings file contents:
                                       {
                                            ""Source"": ""./"",
                                            ""Destination"": ""../app/models/generated"",
                                            ""Files"": [ ""*.dll"" ],
                                            ""ControllerBaseClassNames"": [ ""ApiController"" ],
                                            ""ApiRelativePath"": ""api"",
                                            ""HttpModule"": ""HttpClientModule""
                                            ""CombineImports"": true|false [default: false]
                                        }
      <SOURCE>                The path that contains the .dll(s)
      <DESTINATION>           The destination path where the generated models will be placed
      <APIPATH>               The prefix api calls use (leave blank to not generate a data service)
    ";

		public static int Run(string[] args)
		{
			var sw = Stopwatch.StartNew();

			int loadResult = LoadOptions(args);
			if (loadResult != 0)
			{
				return loadResult;
			}

			_basePath = AbsolutePath(_options.Source);
			AppDomain.CurrentDomain.AssemblyResolve += ResolveAssembly;// Look in the base path for referenced .dll files instead of the startup path

			Console.Write("Scanning for DTO objects in {0}...  ", _basePath);
			var apiControllers = GetApiControllers(_basePath);
			var controllerModels = new HashSet<Type>(apiControllers.SelectMany(GetModelsFromController));// return and parameter models
			var allModels = GetAllModelsToGenerate(controllerModels);
			Console.WriteLine("Found {0}", allModels.Count);

			var targetPath = AbsolutePath(_options.Destination);
			Console.WriteLine("Generating to Target Path: " + targetPath);
			// Invoke all generators and pass the results to the index generator
			List<string> allGeneratedNames;
			if(_options.Generator == "react") {
				allGeneratedNames = IndexGenerator.Generate(targetPath,
					InterfaceGenerator.Generate(targetPath, allModels, _options),
					PromiseDataServiceGenerator.Generate(apiControllers, controllerModels, targetPath, _options)

				).Select(n => n + ".ts").ToList();
			} else {
				allGeneratedNames = IndexGenerator.Generate(targetPath,
					EntityGenerator.Generate(targetPath, allModels, _options),
					DataServiceGenerator.Generate(apiControllers, controllerModels, targetPath, _options)

				).Select(n => n + ".ts").ToList();
			}


            allGeneratedNames.AddRange(SchemaGenerator.Generate(targetPath, allModels, _options));

			RemoveNonGeneratedFiles(targetPath, allGeneratedNames);

			Console.WriteLine("Done in {0:N3}s", sw.Elapsed.TotalSeconds);
			return 0;
		}

		private static int LoadOptions(string[] args)
		{
			var arguments = new Docopt().Apply(usage, args, version: "Typescripter", exit: true);

			// using the settings file.
			ValueObject settingsfile = arguments["<SETTINGSFILE>"];
			if (settingsfile != null && settingsfile.IsString)
			{
				var file = (string)settingsfile.Value;
				if (File.Exists(file))
				{
					try
					{
						_options = FileHandler.ReadJson<Options>(file);
						if (string.IsNullOrEmpty(_options.Source))
						{
							throw new Exception("Source is null or empty in options.");
						}
						if (string.IsNullOrEmpty(_options.Destination))
						{
							throw new Exception("Destination is null or empty in options.");
						}
						if (_options.Files == null || _options.Files.Length <= 0)
						{
							_options.Files = new[] { "*.client.dll" };
						}
						if (_options.ControllerBaseClassNames == null || _options.ControllerBaseClassNames.Length <= 0)
						{
							_options.ControllerBaseClassNames = new[] { "ApiController" };
						}
						if (!_options.CombineImports.HasValue)
						{
							_options.CombineImports = false;
						}
					  if (!_options.HandleErrors.HasValue)
					  {
					    _options.HandleErrors = true;
					  }

                      if (_options.SchemaFilePath == null || _options.SchemaFilePath.Length <= 0)
                      {
                          _options.SchemaFilePath = "./Schema.json";
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
					Console.WriteLine(String.Format("Settings file {0} does not exist.", file));
					return 2;
				}
			}
			else
			{
				ValueObject source = arguments[CommandLineArguments.Source];
				ValueObject destination = arguments[CommandLineArguments.Destination];
				ValueObject files = arguments[CommandLineArguments.Files];
				ValueObject apipath = arguments[CommandLineArguments.Path];
				ValueObject classnames = arguments[CommandLineArguments.ClassNames];
				ValueObject newhttp = arguments[CommandLineArguments.NewHttp];
				ValueObject combineImports = arguments[CommandLineArguments.CombineImports];
                ValueObject generateSchemaJson = arguments[CommandLineArguments.GenerateSchemaJson];
                ValueObject schemaFilePath = arguments[CommandLineArguments.SchemaFilePath];
				ValueObject generator = arguments[CommandLineArguments.Generator];
				_options = new Options
				{
					Source = source != null && source.IsString ? (string)source.Value : "./",
					Destination = destination != null && source.IsString ? (string)destination.Value : "../models/generated",
					ApiRelativePath = apipath != null && apipath.IsString ? (string)apipath.Value : null,
					Files = files != null && files.IsList && files.AsList.Count == 1 ? ((string)(((ValueObject)files.AsList[0]).Value)).Split(',') : new[] { "*.client.dll" },
					ControllerBaseClassNames = classnames != null && classnames.IsList && classnames.AsList.Count == 1 ? ((string)(((ValueObject)classnames.AsList[0]).Value)).Split(',') : new[] { "ApiController" },
					CombineImports = combineImports != null && combineImports.IsTrue ? true : false,
                    SchemaFilePath = schemaFilePath != null ? schemaFilePath.Value as string : "./Schema.json",
                    GenerateSchemaJson = generateSchemaJson != null ? generateSchemaJson.IsTrue : false,
                    Generator = generator != null ? generator.Value as string : null,
				};
			}
			return 0;
		}

		private static List<Type> GetApiControllers(string path)
		{
			var dlls = _options.Files.SelectMany(f => Directory.GetFiles(path, f));// Get dll files from options file list.
			var assemblies = dlls.Select(Load).Where(a => a != null);// Load the assemblies so we can reflect on them

			return assemblies.SelectMany(GetApiControllers).ToList();// Find all Types that inherit from ApiController
		}

		private static HashSet<Type> GetAllModelsToGenerate(HashSet<Type> models)
		{
			var allModels = new HashSet<Type>(GetDerivedTypes(models));// Add derived types

			// Now recursively search the all models for child models
			foreach (var model in allModels.ToArray())
			{
				RecursivelySearchModels(model, allModels);
			}
			return allModels;
		}

		private static Assembly ResolveAssembly(object sender, ResolveEventArgs args)
		{
			try
			{
				var file = Path.Combine(_basePath, args.Name.Substring(0, args.Name.IndexOf(",", StringComparison.Ordinal)) + ".dll");
				return Assembly.LoadFile(file);
			}
			catch
			{
				Console.WriteLine(args.Name);
				return null;
			}
		}

		private static Assembly Load(string path)
		{
			try { return Assembly.LoadFile(path); }
			catch { return null; }
		}

		private static string AbsolutePath(string relativePath)
		{
			return Path.IsPathRooted(relativePath) ? relativePath : Path.Combine(Environment.CurrentDirectory, relativePath);
		}

		private static IEnumerable<Type> GetApiControllers(Assembly a)
		{
			return TryGetApiControllerTypes(a).Where(t => t.BaseType != null && _options.ControllerBaseClassNames.Contains(t.BaseType.Name));
		}

		private static IEnumerable<Type> TryGetApiControllerTypes(Assembly a)
		{
			try { return a.GetTypes(); }
			catch { return Enumerable.Empty<Type>(); }
		}

		private static IEnumerable<Type> GetModelsFromController(Type controllerType)
		{
			var methods = controllerType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
			var models = methods.SelectMany(GetModelsFromMethod);
			return models;
		}

		private static IEnumerable<Type> GetModelsFromMethod(MethodInfo arg)
		{
			return GetModelTypes(arg.ReturnType)
				.Union(arg.GetParameters()
					.Where(p => !HasAttributeNamed(p, "FromUriAttribute")) // All FromUri parameters will be expanded later in the DataServiceGenerator
					.Select(p => p.ParameterType)
					.SelectMany(GetModelTypes)
				);
		}

		private static bool HasAttributeNamed(ParameterInfo parameter, string attributeName)
		{
			var attribs = parameter.GetCustomAttributes(inherit: false);
			return attribs.Length > 0 && attribs.Any(a => a.GetType().Name == attributeName);
		}

		private static HashSet<Type> GetDerivedTypes(HashSet<Type> types)
		{
			var typesList = types;
			var allTypes = new HashSet<Type>(typesList);
			foreach (var type in typesList)
			{
				allTypes.UnionWith(type.Assembly.GetTypes().Where(t => t != type && type.IsAssignableFrom(t)));
			}
			return allTypes;
		}

		private static IEnumerable<Type> GetModelTypes(Type t)
		{
			if (t.GetCustomAttributes().Any(x => x.GetType().Name == "TypeScripterIgnoreAttribute")
				|| t == typeof(Task))
			{
				yield break;
			}

			if (t.IsModelType() || t.IsEnum)
			{
				if (t.IsArray)
				{
					yield return t.GetElementType();
				}
				else
				{
					yield return t;
				}
			}
			else if (t.IsGenericType)
			{
				foreach (var a in t.GetGenericArguments().Where(a => a.IsModelType() || a.IsGenericType).SelectMany(GetModelTypes))
				{
					yield return a;
				}
			}

			if (t.BaseType != null && t.BaseType.IsModelType())
			{
				yield return t.BaseType;
			}
		}

		private static void RecursivelySearchModels(Type model, HashSet<Type> visitedModels)
		{
			var props = model
				.GetProperties()
				.Select(p => p.GetPropertyType()).SelectMany(GetModelTypes).Where(t => !visitedModels.Contains(t) && t.IsModelType());
			foreach (var p in props)
			{
				visitedModels.Add(p);
				RecursivelySearchModels(p, visitedModels);
			}
		}

		private static void RemoveNonGeneratedFiles(string targetPath, List<string> allGeneratedNames)
		{
			// Make sure output directory does not contain any non-generated files that might cause problems
			var filesToDelete = Directory
				.GetFiles(targetPath)
				.Select(Path.GetFileName)
				.Except(allGeneratedNames.Select(m => m), StringComparer.OrdinalIgnoreCase);

			foreach (var file in filesToDelete)
			{
                Console.WriteLine($"Deleting {file}.");
				File.Delete(Path.Combine(targetPath, file));
			}
		}
	}
}