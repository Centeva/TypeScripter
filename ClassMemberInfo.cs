namespace TypeScripter {
	public class ClassMemberInfo {
		public string Name { get; set; }
		public TypeDetails Type { get; set; }

		private string _value;
		public string Value {
			get { return Type.Name == "string" ? "'" + _value + "'" : _value; }
			set { _value = value; }
		}
	}
}