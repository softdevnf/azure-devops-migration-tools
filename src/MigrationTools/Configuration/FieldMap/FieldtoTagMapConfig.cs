﻿namespace MigrationTools.Configuration.FieldMap
{
    public class FieldtoTagMapConfig : IFieldMapConfig
    {
        public string WorkItemTypeName { get; set; }
        public string sourceField { get; set; }
        public string formatExpression { get; set; }
        public string FieldMap
        {
            get
            {
                return "FieldToTagFieldMap";
            }
        }
    }
}
