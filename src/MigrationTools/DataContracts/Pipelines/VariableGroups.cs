﻿using System;
using System.Dynamic;
using Microsoft.Extensions.Logging;

namespace MigrationTools.DataContracts.Pipelines
{

    [ApiPath("distributedtask/variablegroups")]
    [ApiName("Variable Groups")]
    public class VariableGroups : RestApiDefinition
    {
        protected ILogger Log { get; }

        public ExpandoObject Variables { get; set; }
        public string Type { get; set; }
        public Createdby CreatedBy { get; set; }
        public string CreatedOn { get; set; }
        public Modifiedby ModifiedBy { get; set; }
        public string ModifiedOn { get; set; }
        public bool IsShared { get; set; }

        public override bool HasTaskGroups()
        {
            Log.LogError("we currently not support taskgroup nesting.");
            return false;
        }

        public override bool HasVariableGroups()
        {
            Log.LogError("we currently not support variablegroup nesting.");
            return false;
        }

        public override void ResetObject()
        {
            Id = null;
            CreatedBy = null;
            CreatedOn = null;
            ModifiedBy = null;
            ModifiedOn = null;
        }
    }

    public class Createdby
    {
        public string displayName { get; set; }
        public string id { get; set; }
        public string uniqueName { get; set; }
    }

    public class Modifiedby
    {
        public string displayName { get; set; }
        public string id { get; set; }
        public string uniqueName { get; set; }
    }
}
