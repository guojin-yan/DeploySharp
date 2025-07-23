using NUnit.Framework;
using DeploySharp.Log;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DeploySharp.Logger.Tests
{
    [TestFixture()]
    public class LogManagerTests
    {
        [Test()]
        public void InitializeDefaultTest()
        {
            LoggerManager.InitializeDefault();
        }
    }
}