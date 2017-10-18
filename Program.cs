using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using TypeScripter.Generators;

namespace TypeScripter {
	class Program {
		private static string _basePath;
		private static Options _options;

		static int Main(string[] args) {
			var sw = Stopwatch.StartNew();

			int loadResult = LoadOptions(args);
			if(loadResult != 0) {
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
			// Invoke all generators and pass the results to the index generator
			var allGeneratedNames = IndexGenerator.Generate(targetPath,
				EntityGenerator.Generate(targetPath, allModels),
				DataServiceGenerator.Generate(_options.ApiRelativePath, apiControllers, controllerModels, targetPath, _options.HttpModule)
			);
			RemoveNonGeneratedFiles(targetPath, allGeneratedNames);

			Console.WriteLine("Done in {0:N3}s", sw.Elapsed.TotalSeconds);
			return 0;
		}

		private static int LoadOptions(string[] args) {
			if(args.Length != 1 && args.Length > 4) {
				Console.WriteLine("Usage: typescripter.exe <Options File Path>");
				Console.WriteLine("       typescripter.exe <dll source path> <model target path> <api relative path (optional)> <data service type (optional)>");
				Console.WriteLine("Note:  If the <api relative path> is provided, either in the command line or options file, TypeScripter will generate a DataService.ts file.");
                Console.WriteLine("Note:  <data service type> options are httpclientmodule or httpmodule and Correspond to the (new)HttpClientModule or the (old/default)HttpModule");
                Console.WriteLine("Example: typescripter.exe \"Options.json\"");
				Console.WriteLine("         typescripter.exe \"c:\\Source\\Project\\bin\\debug\" \"c:\\Source\\Project\\src\\app\\models\\generated\"");
				Console.WriteLine("------------------------------------");
				for(var i = 0; i < args.Length; i++) {
					Console.WriteLine("[{0}] = {1}", i, args[i]);
				}
				return 1;
			}

			if(args.Length == 1) {
				if(File.Exists(args[0])) {
					try {
						_options = FileHandler.ReadJson<Options>(args[0]);
						if(string.IsNullOrEmpty(_options.Source)) {
							throw new Exception("Source is null or empty in options.");
						}
						if(string.IsNullOrEmpty(_options.Destination)) {
							throw new Exception("Destination is null or empty in options.");
						}
						if(_options.Files == null || _options.Files.Length <= 0) {
							_options.Files = new[] { "*.client.dll" };
						}
						if(_options.ControllerBaseClassNames == null || _options.ControllerBaseClassNames.Length <= 0) {
							_options.ControllerBaseClassNames = new[] { "ApiController" };
						}
						if(string.IsNullOrEmpty(_options.HttpModule))
						{
							_options.HttpModule = "HttpModule";
						}
					}
					catch(Exception e) {
						Console.WriteLine(e.Message);
						return 2;
					}
				}
				else {
					Console.WriteLine("No options file found.");
					return 3;
				}
			}
			else if(args.Length >= 2) {
				_options = new Options {
					Source = args[0],
					Destination = args[1],
					Files = new[] { "*.client.dll" },
					ControllerBaseClassNames = new[] { "ApiController" }
				};
				if(args.Length >= 3) {
					_options.ApiRelativePath = args[2];
                    if(args.Length >= 4)
                    {
                        _options.HttpModule = args[3];
                        if(_options.HttpModule != "HttpClientModule" || _options.HttpModule != "HttpModule")
                        {
                            throw new Exception("HttpModule must be one of HttpModule or HttpClientModule");
                        }
                    }
				}
			}
			return 0;
		}

		private static List<Type> GetApiControllers(string path) {
			var dlls = _options.Files.SelectMany(f => Directory.GetFiles(path, f));// Get dll files from options file list.
			var assemblies = dlls.Select(Load).Where(a => a != null);// Load the assemblies so we can reflect on them

			return assemblies.SelectMany(GetApiControllers).ToList();// Find all Types that inherit from ApiController
		}

		private static HashSet<Type> GetAllModelsToGenerate(HashSet<Type> models) {
			var allModels = new HashSet<Type>(GetDerivedTypes(models));// Add derived types

			// Now recursively search the all models for child models
			foreach(var model in allModels.ToArray()) {
				RecursivelySearchModels(model, allModels);
			}
			return allModels;
		}

		private static Assembly ResolveAssembly(object sender, ResolveEventArgs args) {
			try {
				var file = Path.Combine(_basePath, args.Name.Substring(0, args.Name.IndexOf(",", StringComparison.Ordinal)) + ".dll");
				return Assembly.LoadFile(file);
			}
			catch {
				Console.WriteLine(args.Name);
				return null;
			}
		}

		private static Assembly Load(string path) {
			try { return Assembly.LoadFile(path); }
			catch { return null; }
		}

		private static string AbsolutePath(string relativePath) {
			return Path.IsPathRooted(relativePath) ? relativePath : Path.Combine(Environment.CurrentDirectory, relativePath);
		}

		private static IEnumerable<Type> GetApiControllers(Assembly a) {
			return TryGetApiControllerTypes(a).Where(t => t.BaseType != null && _options.ControllerBaseClassNames.Contains(t.BaseType.Name));
		}

		private static IEnumerable<Type> TryGetApiControllerTypes(Assembly a) {
			try { return a.GetTypes(); }
			catch { return Enumerable.Empty<Type>(); }
		}

		private static IEnumerable<Type> GetModelsFromController(Type controllerType) {
			var methods = controllerType.GetMethods();
			var models = methods.SelectMany(GetModelsFromMethod);
			return models;
		}

		private static IEnumerable<Type> GetModelsFromMethod(MethodInfo arg) {
			return GetModelTypes(arg.ReturnType)
				.Union(arg.GetParameters()
					.Select(p => p.ParameterType)
					.SelectMany(GetModelTypes)
				);
		}

		private static HashSet<Type> GetDerivedTypes(HashSet<Type> types) {
			var typesList = types;
			var allTypes = new HashSet<Type>(typesList);
			foreach(var type in typesList) {
				allTypes.UnionWith(type.Assembly.GetTypes().Where(t => t != type && type.IsAssignableFrom(t)));
			}
			return allTypes;
		}

		private static IEnumerable<Type> GetModelTypes(Type t) {
			if(t.GetCustomAttributes().Any(x => x.GetType().Name == "TypeScripterIgnoreAttribute")) yield break;
			if(t.IsModelType()) {
				if(t.IsArray) {
					yield return t.GetElementType();
				}
				else {
					yield return t;
				}
			}
			else {
				if(t.IsGenericType) {
					foreach(var a in t.GetGenericArguments().Where(a => a.IsModelType()).SelectMany(GetModelTypes)) {
						yield return a;
					}
				}
			}
			if(t.BaseType != null && t.BaseType.IsModelType()) {
				yield return t.BaseType;
			}
		}

		private static void RecursivelySearchModels(Type model, HashSet<Type> visitedModels) {
			var props = model
				.GetProperties()
				.Select(p => p.GetPropertyType()).SelectMany(GetModelTypes).Where(t => !visitedModels.Contains(t) && t.IsModelType());
			foreach(var p in props) {
				visitedModels.Add(p);
				RecursivelySearchModels(p, visitedModels);
			}
		}

		private static void RemoveNonGeneratedFiles(string targetPath, List<string> allGeneratedNames) {
			// Make sure output directory does not contain any non-generated files that might cause problems
			var filesToDelete = Directory
				.GetFiles(targetPath)
				.Select(Path.GetFileName)
				.Except(allGeneratedNames.Select(m => m + ".ts"), StringComparer.OrdinalIgnoreCase);

			foreach(var file in filesToDelete) {
				File.Delete(Path.Combine(targetPath, file));
			}
		}
	}
}