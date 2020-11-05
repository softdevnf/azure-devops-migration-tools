﻿using MigrationTools._EngineV1.Configuration;

namespace MigrationTools.Tests
{
    public class FakeMigrationClientConfig : IMigrationClientConfig
    {
        public IMigrationClientConfig PopulateWithDefault()
        {
            return this;
        }

        public override string ToString()
        {
            return "FakeMigration";
        }
    }
}