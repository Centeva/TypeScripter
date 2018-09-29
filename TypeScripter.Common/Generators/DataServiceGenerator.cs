using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace TypeScripter.Common.Generators
{
	public static class DataServiceGenerator
	{
		public static string HttpClient = "HttpClientModule";
		public static string Http = "HttpModule";

		public static List<string> Generate(List<Type> apiControllers, HashSet<Type> models, string targetPath, Options options)
		{
		  var apiRelativePath = options.ApiRelativePath;
		  var httpModule = options.HttpModule;
		  var combineImports = options.CombineImports ?? false;
		  var handleErrors = options.HandleErrors ?? false;
      
      if (string.IsNullOrWhiteSpace(apiRelativePath))
			{
				return new List<string>();
			}
			const string methodTemplate = "\t\t{0}: ({1}): Observable<{2}> => this.http.{3}(`{4}`{5}).pipe(map((res: Response) => {6}), catchError(this.handleError)),";
			const string clientMethodTemplate = "\t\t{0}: ({1}): Observable<{2}> => this.http.{3}{4}(`{5}`{6}{7}.pipe(catchError(this.handleError)),";

			if (options.ApiRelativePath.EndsWith("/"))
			{
				apiRelativePath = apiRelativePath.Substring(0, apiRelativePath.Length - 1);
			}

			var sb = new StringBuilder();

			sb.AppendLine("// tslint:disable:max-line-length");
			sb.AppendLine("// tslint:disable:member-ordering");
			sb.AppendLine("import { Injectable } from '@angular/core';");
			sb.AppendLine(httpModule == HttpClient ? "import { HttpClient, HttpErrorResponse } from '@angular/common/http';" : "import { Http, Response } from '@angular/http';");
			sb.AppendLine("import { Observable, throwError } from 'rxjs';");
			sb.AppendLine("import { map, catchError } from 'rxjs/operators';");
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
					sb.AppendLine(string.Format("{0} {1} {2}", startingImport, m.Name, endingImport));
				}
			}
			sb.AppendLine("");

			sb.AppendLine("@Injectable()");
			sb.AppendLine("export class DataService {");

			sb.AppendLine(httpModule == HttpClient ? "\tconstructor(private http: HttpClient) {}" : "\tconstructor(private http: Http) {}");
			sb.AppendLine();
			sb.AppendLine(string.Format("\tapiRelativePath: string = '{0}';", apiRelativePath));
			sb.AppendLine();

			int methodCount = 0;

			foreach (var apiController in apiControllers)
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
					if (httpMethodName != "post")
					{
						if (parameters.Length > 0)
						{
							url = url + "?" + string.Join("&", parameters.Select(GenerateGetString));
						}
						if (httpModule == Http) { sb.AppendFormat(methodTemplate, method.Name.Camelize(), joinedParameters, method.ReturnType.ToTypeScriptType(), httpMethodName, url, "", GetResultMapperExpression(method.ReturnType)); }
						else { sb.AppendFormat(clientMethodTemplate, method.Name.Camelize(), joinedParameters, method.ReturnType.ToTypeScriptType(), httpMethodName, AddReturnType(method.ReturnType.ToTypeScriptType()), url, "", AddResponseType(method.ReturnType.ToTypeScriptType())); }
						sb.AppendLine();
					}
					else
					{
						var allParameters = parameters;

						if (allParameters.Length > 1)
						{
							Console.WriteLine("/* WARNING! Only POST methods with a zero or one parameter are currently supported -- {0}.{1} */", method.DeclaringType, method.Name);
						}
						if (httpModule == Http)
						{
							sb.AppendFormat(methodTemplate,
							method.Name.Camelize(),
							joinedParameters,
							method.ReturnType.ToTypeScriptType(),
							httpMethodName,
							url,
							allParameters.Length > 0 ? ", " + allParameters[0].Name : ", null",
							GetResultMapperExpression(method.ReturnType));
						}
						else
						{
							sb.AppendFormat(clientMethodTemplate,
							method.Name.Camelize(),
							joinedParameters,
							method.ReturnType.ToTypeScriptType(),
							httpMethodName,
							AddReturnType(method.ReturnType.ToTypeScriptType()),
							url,
							allParameters.Length > 0 ? ", " + allParameters[0].Name : ", null",
							AddResponseType(method.ReturnType.ToTypeScriptType()));
						}
						sb.AppendLine();
					}
				}
				sb.AppendLine("\t};");
			}

			sb.AppendLine();
			sb.AppendLine(httpModule == Http ? "\tprivate handleError(error: Response | any) {" : "\tprivate handleError(error: HttpErrorResponse | any) {");
		  if (handleErrors)
		  {
		    sb.AppendLine("\t\tlet errMsg: string;");
		    if (httpModule == Http)
		    {
		      sb.AppendLine("\t\tif (error instanceof Response) {");
		      sb.AppendLine("\t\t\tconst hdr = error.headers.get('ExceptionMessage');");
		      sb.AppendLine("\t\t\tconst body = error.json() || '';");
		      sb.AppendLine("\t\t\tconst err = body.error || JSON.stringify(body);");
		      sb.AppendLine("\t\t\terrMsg = `${error.status}${hdr ? ' - ' + hdr : ''} - ${error.statusText || ''} ${err}`;");
		      sb.AppendLine("\t\t} else {");
		      sb.AppendLine("\t\t\terrMsg = error.message ? error.message : error.toString();");
		      sb.AppendLine("\t\t}");
		    }
		    else
		    {
		      sb.AppendLine("\t\tif (error instanceof HttpErrorResponse) {");
		      sb.AppendLine("\t\t\tconst hdr = error.headers.get('ExceptionMessage');");
		      sb.AppendLine(
		        "\t\t\terrMsg = `${error.status}${hdr ? ' - ' + hdr : ''} - ${error.statusText || ''} ${error.error}`;");
		      sb.AppendLine("\t\t} else {");
		      sb.AppendLine("\t\t\terrMsg = error.message ? error.message : error.toString();");
		      sb.AppendLine("\t\t}");
		    }

		    sb.AppendLine("\t\tconsole.error(errMsg);");
		    sb.AppendLine("\t\treturn throwError(errMsg);");
		  }
		  else
		  {
		    sb.AppendLine("\t\treturn throwError(error);");
      }

		  sb.AppendLine("\t}");
			sb.AppendLine("}");

			Utils.WriteIfChanged(sb.ToString(), Path.Combine(targetPath, "DataService.ts"));
			Console.WriteLine("Generated a data service with {0} controllers and {1} methods.", apiControllers.Count, methodCount);
			return new List<string> { "DataService" };
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

		private static readonly HashSet<string> _primitiveTypes = new HashSet<string>{"boolean", "boolean[]", "number", "number[]", "string", "string[]"};
		
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
			if (typeDetails.Name == "boolean" || typeDetails.Name == "string" || typeDetails.ToString() == "number")
			{
				string convert = "x";
				if (typeDetails.Name == "boolean") { convert = "JSON.parse(x) === true"; }
				else if (typeDetails.Name == "number") { convert = "Number(x)"; }
				string map = String.Format(".pipe(map( x => {0}))", convert);
				return ", { responseType: 'text' })" + map;
			}
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
	}
}
