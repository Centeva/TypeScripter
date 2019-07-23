using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TypeScripter.Common.Generators;

namespace TypeScripter.Common
{
	internal class PromiseDataServiceGenerator
	{

		public static List<string> Generate(List<Type> apiControllers, HashSet<Type> models, string targetPath, Options options)
		{
			var apiRelativePath = options.ApiRelativePath;
			var combineImports = options.CombineImports ?? true;

			if (string.IsNullOrWhiteSpace(apiRelativePath))
			{
				return new List<string>();
			}

			if (options.ApiRelativePath.EndsWith("/"))
			{
				apiRelativePath = apiRelativePath.Substring(0, apiRelativePath.Length - 1);
			}

			var sb = new StringBuilder();

			sb.AppendLine("// tslint:disable:max-line-length");
			sb.AppendLine("// tslint:disable:member-ordering");
			sb.AppendLine("import * as moment from 'moment';");

			var startingImport = "import {";
			var endingImport = "} from '.';";

			if (combineImports)
			{
				sb.AppendLine(string.Format("{0}", startingImport));
				foreach (var m in models.OrderBy(m => m.Name))
				{
					sb.AppendLine(string.Format("\t{0},", m.Name));
				}
				sb.AppendLine(string.Format("{0}", endingImport));
			}
			else
			{
				foreach (var m in models.OrderBy(m => m.Name))
				{
					sb.AppendLine($"import {{ {m.Name}, {m.Name}_fromJsonObj }} from '.'");
				}
			}
			sb.AppendLine("");


			sb.AppendLine(@"export interface ITypescripterHttpClient {
	get<T>(path: string, params?: { [k: string]: string | number }, opts?: { responseType: 'text' }): Promise<T>;
	post<T>(path: string, body: any, opts?: { responseType: 'text' }): Promise<T>;
	put<T>(path: string, body: any, opts?: { responseType: 'text' }): Promise<T>;
	delete<T>(path: string, params?: { [k: string]: string | number }, opts?: { responseType: 'text' }): Promise<T>;
}");




	sb.AppendLine("");

			sb.AppendLine("export class DataService {");

			sb.AppendLine("\tconstructor(private http: ITypescripterHttpClient) {}");
			sb.AppendLine();
			sb.AppendLine(string.Format("\tapiRelativePath: string = '{0}';", apiRelativePath));
			sb.AppendLine();

			int methodCount = 0;


			foreach (var apiController in apiControllers.OrderBy(c => c.Name))
			{
				sb.AppendFormat("\t{0} = {{{1}", apiController.Name.Substring(0, apiController.Name.Length - "Controller".Length).Camelize(), Environment.NewLine);

				var methods = apiController.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);
				methodCount += methods.Length;
				HashSet<string> names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				foreach (var method in methods.OrderBy(m => m.Name))
				{
					// TODO: don't try to generate overloaded methods, instead print out a warning message when that happens
					if (names.Contains(method.Name))
					{
						sb.AppendLine("ERROR! The controller '" + apiController.Name + "' has a duplicate method name: '" + method.Name + "'");
					}
					names.Add(method.Name);
					var parameters = ExpandFromUriParameters(method.GetParameters());
					var joinedParameters = string.Join(", ", parameters.Select(p => p.Name + ": " + p.ParameterType.ToTypeScriptType()));
					var url = CombineUri("${this.apiRelativePath}", apiController.Name.Substring(0, apiController.Name.Length - "Controller".Length), method.Name);
					var httpMethodName = GetHttpMethodName(method);
					if (httpMethodName == null) { continue; }

					const string clientMethodTemplate = "\t\t{0}: ({1}): Promise<{2}> => this.http.{3}{4}(`{5}`{6}{7}{8},";
					var returnType = method.ReturnType.ToTypeScriptType();

					if (httpMethodName != "post")
					{
						if (parameters.Length > 0)
						{
							url = url + "?" + string.Join("&", parameters.Select(GenerateGetString));
						}

						sb.AppendFormat(clientMethodTemplate,
							/*0*/method.Name.Camelize(),
							/*1*/joinedParameters,
							/*2*/returnType,
							/*3*/httpMethodName,
							/*4*/AddReturnType(returnType),
							/*5*/url,
							/*6*/"",
							/*7*/AddResponseType(returnType),
							/*8*/GetCallConstructorStr(method.ReturnType)
						);
						sb.AppendLine();
					}
					else
					{
						if (parameters.Length > 1)
						{
							Console.WriteLine("/* WARNING! Only POST methods with a zero or one parameter are currently supported -- {0}.{1} */", method.DeclaringType, method.Name);
						}

						sb.AppendFormat(clientMethodTemplate,
							/*0*/method.Name.Camelize(),
							/*1*/joinedParameters,
							/*2*/returnType,
							/*3*/httpMethodName,
							/*4*/AddReturnType(returnType),
							/*5*/url,
							/*6*/parameters.Length > 0 ? ", " + parameters[0].Name : ", null",
							/*7*/AddResponseType(returnType),
							/*8*/GetCallConstructorStr(method.ReturnType)
						);
						sb.AppendLine();
					}
				}
				sb.AppendLine("\t};");
			}

			sb.AppendLine();
			sb.AppendLine("}");

			Utils.WriteIfChanged(sb.ToString(), Path.Combine(targetPath, "DataService.ts"));
			Console.WriteLine("Generated a data service with {0} controllers and {1} methods.", apiControllers.Count, methodCount);
			return new List<string> { "DataService" };
		}

		private static object ImportsTemplate()
		{
			throw new NotImplementedException();
		}

		private class ParameterEssentials
		{
			public string Name { get; set; }
			public Type ParameterType { get; set; }
			public Type[] Attributes { get; set; }

			public static ParameterEssentials FromParameterInfo(ParameterInfo i)
			{
				return new ParameterEssentials
				{
					Name = i.Name,
					ParameterType = i.ParameterType,
					Attributes = i.GetCustomAttributes(inherit: false).Select(a => a.GetType()).ToArray()
				};
			}
		}

		private static readonly HashSet<string> _primitiveTypes = new HashSet<string> { "boolean", "boolean[]", "number", "number[]", "string", "string[]" };

		private static ParameterEssentials[] ExpandFromUriParameters(ParameterInfo[] parameters)
		{
			Func<ParameterEssentials, bool> isPrimitive = p => _primitiveTypes.Contains(p.ParameterType.ToTypeScriptType().Name);
			Func<ParameterEssentials, bool> isBareParameter = p => isPrimitive(p) || !p.Attributes.Any(_ => _.Name == "FromUriAttribute");

			var bareParameters = parameters
				.Select(ParameterEssentials.FromParameterInfo)
				.Where(p => isBareParameter(p))
				.ToList();

			var fromUriParameters = parameters
				.Select(ParameterEssentials.FromParameterInfo)
				.Where(p => !isBareParameter(p))
				.SelectMany(p =>
					p.ParameterType
						.GetProperties()
						.Select(prop =>
							new ParameterEssentials
							{
								Name = prop.Name.Camelize(),
								ParameterType = prop.PropertyType,
								Attributes = new Type[0]
							}))
				.ToList();

			return bareParameters.Union(fromUriParameters).ToArray();
		}

		private static string AddResponseType(TypeDetails typeDetails)
		{
			//if (typeDetails.Name == "boolean" || typeDetails.Name == "string" || typeDetails.ToString() == "number")
			//{
			//	string convert = "x";
			//	if (typeDetails.Name == "boolean") { convert = "JSON.parse(x) === true"; }
			//	else if (typeDetails.Name == "number") { convert = "Number(x)"; }
			//	string map = String.Format(".then( x => {0})", convert);
			//	return ", { responseType: 'text' })" + map;
			//}
			return ")";
		}

		private static object AddReturnType(TypeDetails typeDetails)
		{
			if (typeDetails.ToString() == "boolean" || typeDetails.ToString() == "string" || typeDetails.ToString() == "number")
			{
				return "";
			}
			return String.Format("<{0}>", typeDetails.Name);
		}

		private static string GenerateGetString(ParameterEssentials p)
		{
			if (p.ParameterType == typeof(DateTime))
			{
				return string.Format("{0}=${{{0}.toISOString()}}", p.Name);
			}
			else if (p.ParameterType.ToTypeScriptType().Name.EndsWith("[]"))
			{
				return string.Format("${{{0}.map(x => `{0}=${{x}}`).join('&')}}", p.Name);
			}
			return string.Format("{0}=${{{0}}}", p.Name);
		}

		private static string CombineUri(params string[] parts)
		{
			StringBuilder sb = new StringBuilder();
			sb.Append(parts[0]);
			for (int i = 1; i < parts.Length; i++)
			{
				if (!parts[i - 1].EndsWith("/"))
				{
					sb.Append('/');
				}
				sb.Append(parts[i].TrimStart('/'));
			}
			return sb.ToString();
		}

		private static string GetResultMapperExpression(Type returnType)
		{
			if (typeof(IEnumerable<object>).IsAssignableFrom(returnType) && returnType.GetGenericArguments()[0].IsModelType())
			{
				return string.Format("res.json().map(r => new {0}(r))", returnType.GetGenericArguments()[0].Name);
			}
			if (returnType.IsModelType())
			{
				return string.Format("new {0}(res.json())", returnType.Name);
			}
			return "res.json()";
		}

		private static string GetHttpMethodName(MethodInfo method)
		{
			var attributes = method.GetCustomAttributes().Select(a => a.GetType().Name).ToList();
			foreach (var a in attributes)
			{
				if (a == "HttpGetAttribute") return "get";
				if (a == "HttpPutAttribute") return "put";
				if (a == "HttpPostAttribute") return "post";
				if (a == "HttpDeleteAttribute") return "delete";
			}

			if (method.Name.StartsWith("Get")) return "get";
			if (method.Name.StartsWith("Put")) return "put";
			if (method.Name.StartsWith("Post")) return "post";
			if (method.Name.StartsWith("Update")) return "post";
			if (method.Name.StartsWith("Delete")) return "delete";

			Console.WriteLine("WARNING:  The method '{0}.{1}' does not have a recognizable HTTP method.", method.DeclaringType.Name, method.Name);

			return null;
		}

		public static string GetCallConstructorStr(Type returnType)
		{
			string typescriptType = returnType.ToTypeScriptType().Name;

			// Make sure the DTO constructor gets called in the pipeline
			if (returnType.IsOrContainsModelType())
			{
				if (typescriptType.EndsWith("[]"))
				{
					var elementType = typescriptType.Substring(0, typescriptType.Length - 2); // Take off the '[]' at the end of the type
					return $".then(value => value.map(x => {elementType}_fromJsonObj(x)))";
				}
				else
				{
					return $".then(value => {typescriptType}_fromJsonObj(value))";
				}
			}
			return "";
		}
	}
}