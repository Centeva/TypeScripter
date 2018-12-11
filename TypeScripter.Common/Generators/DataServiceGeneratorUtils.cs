using System;
using System.Collections.Generic;

namespace TypeScripter.Common.Generators
{
    public class DataServiceGeneratorUtils
    {
        public static string GetPipeString(Type returnType)
        {
            return string.Join(".", GetPipelineSteps(returnType));
        }

        private static IEnumerable<string> GetPipelineSteps(Type returnType)
        {
            string typescriptType = returnType.ToTypeScriptType().Name;

            // Make sure the DTO constructor gets called in the pipeline
            if (returnType.IsOrContainsModelType())
            {
                if (typescriptType.EndsWith("[]"))
                {
                    // Arrays need both the map pipeline step as well as the array map to call constructors
                    var elementType = typescriptType.Substring(0, typescriptType.Length - 2); // Take off the '[]' at the end of the type
                    yield return $"pipe(map(value => Array.isArray(value) ? value.map(x => new {elementType}(x)) : null))";
                }
                else
                {
                    // Use the rxjs map to call the model's constructor
                    yield return $"pipe(map(value => new {typescriptType}(value)))";
                }
            }
            yield return "pipe(catchError(this.handleError))"; // This is always the final step in the pipeline
        }
    }
}