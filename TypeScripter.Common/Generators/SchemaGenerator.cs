using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Newtonsoft.Json;
using TypeScripter.Attributes;
 
internal class SchemaFieldModel {
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
}

internal class SchemaModel
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string QualifiedName { get; set; }
    
    public List<SchemaFieldModel> Fields { get; set; }
    public bool IsClass { get; set; }
    public string Description { get; set; }
}

namespace TypeScripter.Common.Generators
{
    
    public static class SchemaGenerator {

        public static List<string> Generate(string targetPath, HashSet<Type> allModels, Options options)
        {
            var results = new List<string>();
            if (options.GenerateSchemaJson != true)
                return results;

            var typeLookup = new Dictionary<Guid, Type>();

            List<SchemaModel> GetTypes(HashSet<Type> checkModels)
            {
                var s = new List<SchemaModel>();

                foreach (var model in checkModels)
                {
                    var docAttribute = model.GetCustomAttribute<TypescripterDocAttribute>();

                    var memberList = new List<Type>();
                    memberList.AddRange(model.GetFields().Select(f => f.FieldType));
                    memberList.AddRange(model.GetProperties().Select(p => p.PropertyType));

                    s.Add(new SchemaModel
                    {
                        Id = model.GUID,
                        Name = model.Name,
                        QualifiedName = model.AssemblyQualifiedName,
                        Fields = memberList.Select(f =>
                        {
                            var propAttribute = f.GetCustomAttribute<TypescripterDocAttribute>();
                            if(!typeLookup.ContainsKey(f.GUID))
                                typeLookup.Add(f.GUID, f);

                            return new SchemaFieldModel
                            {
                                Id = f.GUID,
                                Name = f.Name,
                                Description = propAttribute?.Description
                            };
                        }).ToList(),
                        Description = docAttribute?.Description,
                        IsClass = model.IsClass
                    });
                }

                return s;
            }

            var schema = new List<SchemaModel>(GetTypes(allModels));

            var count = 0;
            var check = true;
            while (check)
            {
                if(count > 100)
                    throw new Exception("ERROR: Schema generator is caught in a loop!");
                var missingFields = new HashSet<Guid>(schema.SelectMany(s => s.Fields.Select(f => f.Id)).Where(fId => schema.All(l => l.Id != fId)));
                var missingTypes = typeLookup.Where(t => missingFields.Contains(t.Key));
                schema.AddRange(GetTypes(new HashSet<Type>(missingTypes.Select(t => t.Value))));
                count++;
                check = schema.SelectMany(s => s.Fields).Any(f => schema.All(s => s.Id != f.Id));
            }

            var json = JsonConvert.SerializeObject(schema);
            var jsonPath = Path.Combine(targetPath, options.SchemaFilePath);

            if(!jsonPath.EndsWith(".json"))
                throw new Exception("SchemaFilePath is not the expected format. Example: './Schema.json'");
            Utils.WriteIfChanged(json, jsonPath);
            Console.WriteLine($"Generated schema for {schema.Count} models.");

            results.Add(Path.GetFileName(jsonPath));

            return results;
        }
    }
}
