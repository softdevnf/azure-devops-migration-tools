﻿using Microsoft.ApplicationInsights.WindowsServer;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MigrationTools;
using MigrationTools.Core.Configuration.Tests;
using System;
using System.Collections.Generic;
using System.Text;

namespace MigrationTools.Tests
{
    [TestClass()]
    public class MigrationHostTests
    {
        [TestMethod()]
        public void MigrationHostTest()
        {
            MigrationEngine mh = new MigrationEngine(null, null, null, new EngineConfigurationBuilderStub());

        }


    }
}