using NUnit.Framework;
using DeploySharp.Log;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Assert = NUnit.Framework.Assert;

namespace DeploySharp.Logger.Tests
{
    [TestFixture()]
    public class LoggerTests
    {
        [SetUp]
        public void Setup()
        {
            LoggerManager.Initialize(LogLevel.DEBUG, LogOutput.All, "CustomLogs");
        }
    

        [Test()]
        public void SetLevelTest()
        {
            Assert.Fail();
        }
    }
}