using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
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

                    Func<T, T> AddToLookup<T>() where T: Type 
                    {
                        return t => {
                            if (!typeLookup.ContainsKey(t.GUID))
                                typeLookup.Add(t.GUID, t);

                            return t;
                        };
                    }

                    

                    var memberList = new List<SchemaFieldModel>();
                    memberList.AddRange(model.GetFields().Select(f =>
                    {
                        if (!typeLookup.ContainsKey(f.FieldType.GUID))
                            typeLookup.Add(f.FieldType.GUID, f.FieldType);
                        return f;
                    }).Select(f => new SchemaFieldModel
                    {
                        Id = f.FieldType.GUID,
                        Name = f.Name,
                        Description = f.GetCustomAttribute<TypescripterDocAttribute>()?.Description
                    }));
                    memberList.AddRange(model.GetProperties().Select(p =>
                    {
                        if (!typeLookup.ContainsKey(p.PropertyType.GUID))
                            typeLookup.Add(p.PropertyType.GUID, p.PropertyType);
                        return p;
                    }).Select(p => new SchemaFieldModel
                    {
                        Id = p.PropertyType.GUID,
                        Name = p.Name,
                        Description = p.GetCustomAttribute<TypescripterDocAttribute>()?.Description
                    }));

                    s.Add(new SchemaModel
                    {
                        Id = model.GUID,
                        Name = model.Name,
                        QualifiedName = model.AssemblyQualifiedName,
                        Fields = memberList.ToList(),
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
