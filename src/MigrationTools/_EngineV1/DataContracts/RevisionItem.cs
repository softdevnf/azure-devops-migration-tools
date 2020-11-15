﻿using System;

namespace MigrationTools._EngineV1.DataContracts
{
    public class RevisionItem
    {
        public int Index { get; set; }
        public int Number { get; set; }
        public DateTime ChangedDate { get; set; }
        public string Type { get; set; }
        public object Fields { get; set; }
    }
}