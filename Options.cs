using System.Runtime.Serialization;

namespace TypeScripter
{
	[DataContract]
	public class Options
	{
		[DataMember] public string Source { get; set; }
		[DataMember] public string Destination { get; set; }
		[DataMember] public string[] Files { get; set; }
		[DataMember] public string[] ControllerBaseClassNames { get; set; }
		[DataMember] public string ApiRelativePath { get; set; }
	}
}