using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace TypeScripter {
	public static class FileHandler {
		public static T ReadJson<T>(string filePath) where T : class {
			try {
				if (File.Exists(filePath)) {
					var serializer = new DataContractJsonSerializer(typeof(T));
					var jsonBytes = Encoding.UTF8.GetBytes(File.ReadAllText(filePath));
					using (var stream = new MemoryStream(jsonBytes))
					{
						return (T)serializer.ReadObject(stream);
					}
				}
			}
			catch (Exception ex) {
				throw new Exception("Failed to parse json", ex);
			}
			return null;
		}
	}
}
