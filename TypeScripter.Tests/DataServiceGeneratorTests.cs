using System;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypeScripter.Common.Generators;

namespace TypeScripter.Tests
{
    [TestClass]
    public class DataServiceGeneratorTests
    {
        [AttributeUsage(AttributeTargets.Parameter)]
        private class FromUriAttribute : Attribute
        {
            // Note: TypeScripter is only looking for this attribute by name, not by namespace so I'm creating a test one here
        }

        private class ExpandUriParametersClass
        {
            public void Foo([FromUri] DateTime dt) { }
        }

        [TestMethod]
        public void ExpandFromUriParameters_DatesConsideredPrimitives()
        {
            // Note: There was a bug where ([FromUri] DateTime dt) parameters would be expanded to have all of the DateTime properties
            //       This was not the desired behavior and was especially bad when there were two DateTime parameters (e.g. startDate, endDate)
            //       because it would cause all of those property names to be duplicated which would cause the TypeScript build to fail.
            //       This test ensures that only a single parameter will be emitted.

            ParameterInfo[] parameters = typeof(ExpandUriParametersClass).GetMethod("Foo").GetParameters();

            var result = DataServiceGenerator.ExpandFromUriParameters(parameters);

            Assert.AreEqual(1, result.Length, "Expected one result");
            Assert.AreEqual(typeof(DateTime), result[0].ParameterType, nameof(DataServiceGenerator.ParameterEssentials.ParameterType));
        }
    }
}