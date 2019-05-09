using System;

namespace TypeScripter.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum | AttributeTargets.Method |
                    AttributeTargets.Interface)]
    public class TypescripterDocAttribute : System.Attribute
    {
        public string Description { get; set; }
    }
}