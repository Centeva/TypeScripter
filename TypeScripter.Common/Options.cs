using System.Runtime.Serialization;

namespace TypeScripter.Common
{
	[DataContract]
	public class Options
	{
		[DataMember] public string Source { get; set; }
		[DataMember] public string Destination { get; set; }
		[DataMember] public string[] Files { get; set; }
		[DataMember] public string[] ControllerBaseClassNames { get; set; }
		[DataMember] public string ApiRelativePath { get; set; }
		[DataMember] public bool? CombineImports { get; set; }
		[DataMember] public bool? HandleErrors { get; set; }
		[DataMember] public bool? GenerateSchemaJson { get; set; }
		[DataMember] public string SchemaFilePath { get; set; }
		[DataMember] public string Generator { get; set; }
	}
}