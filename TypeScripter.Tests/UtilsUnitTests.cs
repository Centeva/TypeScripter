using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TypeScripter.Common;

namespace TypeScripter.Tests
{
	[TestClass]
	public class UtilsUnitTests
	{
		[TestMethod]
		public void ToTypeScripterType_ListOfInt()
		{
			Assert.AreEqual("number[]", typeof(List<int>).ToTypeScriptType().Name);
		}

		[TestMethod]
		public void ToTypeScripterType_ListOfObject()
		{
			Assert.AreEqual("UtilsUnitTests[]", typeof(List<UtilsUnitTests>).ToTypeScriptType().Name);
		}

		[TestMethod]
		public void ToTypeScripterType_ArrayOfInt()
		{
			Assert.AreEqual("number[]", typeof(int[]).ToTypeScriptType().Name);
		}

		[TestMethod]
		public void ToTypeScripterType_ArrayOfObject()
		{
			Assert.AreEqual("UtilsUnitTests[]", typeof(UtilsUnitTests[]).ToTypeScriptType().Name);
		}

		[TestMethod]
		public void ToTypeScripterType_IEnumerableOfInt()
		{
			Assert.AreEqual("number[]", typeof(IEnumerable<int>).ToTypeScriptType().Name);
		}

		[TestMethod]
		public void ToTypeScripterType_Dictionary_Int_Object()
		{
			Assert.AreEqual("any", typeof(Dictionary<int, UtilsUnitTests>).ToTypeScriptType().Name);
		}

		[TestMethod]
		public void ToTypeScripterType_IDictionary_Int_Object()
		{
			Assert.AreEqual("any", typeof(IDictionary<int, UtilsUnitTests>).ToTypeScriptType().Name);
		}
        
	    [TestMethod]
	    public void IsOrContainsModelType_Int()
	    {
	        Assert.IsFalse(typeof(int).IsOrContainsModelType());
	    }

        [TestMethod]
	    public void IsOrContainsModelType_IntArray()
	    {
            Assert.IsFalse(typeof(int[]).IsOrContainsModelType());
	    }

	    [TestMethod]
	    public void IsOrContainsModelType_Bool()
	    {
	        Assert.IsFalse(typeof(bool).IsOrContainsModelType());
	    }

	    [TestMethod]
	    public void IsOrContainsModelType_BoolArray()
	    {
	        Assert.IsFalse(typeof(bool[]).IsOrContainsModelType());
	    }

        [TestMethod]
	    public void IsOrContainsModelType_String()
	    {
	        Assert.IsFalse(typeof(string).IsOrContainsModelType());
	    }

	    [TestMethod]
	    public void IsOrContainsModelType_StringArray()
	    {
	        Assert.IsFalse(typeof(string[]).IsOrContainsModelType());
	    }

	    [TestMethod]
	    public void IsOrContainsModelType_DateTime()
	    {
	        Assert.IsFalse(typeof(DateTime).IsOrContainsModelType());
	    }

	    [TestMethod]
	    public void IsOrContainsModelType_DateTimeArray()
	    {
	        Assert.IsFalse(typeof(DateTime[]).IsOrContainsModelType());
	    }

        public class TestModel {}

	    [TestMethod]
	    public void IsOrContainsModelType_SingleModel()
	    {
	        Assert.IsTrue(typeof(TestModel).IsOrContainsModelType());
	    }

	    [TestMethod]
	    public void IsOrContainsModelType_ArrayOfModel()
	    {
	        Assert.IsTrue(typeof(TestModel[]).IsOrContainsModelType());
	    }

	    [TestMethod]
	    public void IsOrContainsModelType_TaskOfModel()
	    {
	        Assert.IsTrue(typeof(Task<TestModel>).IsOrContainsModelType());
	    }

	    [TestMethod]
	    public void IsOrContainsModelType_ListOfModel()
	    {
	        Assert.IsTrue(typeof(List<TestModel>).IsOrContainsModelType());
	    }

	    [TestMethod]
	    public void IsOrContainsModelType_TaskOfListOfModel()
	    {
	        Assert.IsTrue(typeof(Task<List<TestModel>>).IsOrContainsModelType());
	    }
    }
}
