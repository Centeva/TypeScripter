using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace TypeScripter {
	public static class FileHandler {

		public static T ReadJson<T>(string filePath) where T : class {
			try {
				if (File.Exists(filePath)) {
					var fileContents = File.ReadAllText(filePath);
					return JsonConvert.DeserializeObject<T>(fileContents);
				}
			}
			catch (Exception ex) {
				throw new Exception("Failed to parse json.");
			}
			return null;
		}
	}
}
