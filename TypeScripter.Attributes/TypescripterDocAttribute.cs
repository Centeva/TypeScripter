using System;

namespace TypeScripter.Attributes
{
    [AttributeUsage(AttributeTargets.All)]
    public class TypescripterDocAttribute : System.Attribute
    {
        public string Description { get; set; }
    }
}