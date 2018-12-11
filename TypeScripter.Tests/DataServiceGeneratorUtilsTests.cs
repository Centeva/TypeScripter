using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypeScripter.Common.Generators;

namespace TypeScripter.Tests
{
    [TestClass]
    public class DataServiceGeneratorUtilsTests
    {
        private class ModelType { }

        [TestMethod]
        public void GetPipeStringTest_SingleModel()
        {
            var result = DataServiceGeneratorUtils.GetPipeString(typeof(ModelType));
            Assert.AreEqual("pipe(map(value => new ModelType(value))).pipe(catchError(this.handleError))", result);
        }

        [TestMethod]
        public void GetPipeStringTest_ArrayOfModel()
        {
            var result = DataServiceGeneratorUtils.GetPipeString(typeof(ModelType[]));
            Assert.AreEqual("pipe(map(value => Array.isArray(value) ? value.map(x => new ModelType(x)) : null)).pipe(catchError(this.handleError))", result);
        }
    }
}