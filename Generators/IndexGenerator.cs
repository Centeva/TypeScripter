using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TypeScripter.Generators
{
	public static class IndexGenerator
	{
		public static List<string> Generate(string targetPath, params List<string>[] entries)
		{
			List<string> combined = entries.SelectMany(e => e).OrderBy(e => e).ToList();
			string contents = string.Join(Environment.NewLine, combined.Select(e => string.Format("export * from './{0}';", e)));
			string indexPath = Path.Combine(targetPath, "index.ts");
			Utils.WriteIfChanged(contents, indexPath);

			Console.WriteLine("Created index.ts file with {0} entries", entries.Sum(e => e.Count));
			combined.Add("index");
			return combined;
		}
	}
}