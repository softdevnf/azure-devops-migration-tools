﻿using MigrationTools.DataContracts;

namespace MigrationTools._EngineV1.Enrichers
{
    public interface IAttachmentMigrationEnricher
    {
        void ProcessAttachemnts(WorkItemData source, WorkItemData target, bool save = true);

        void CleanUpAfterSave();
    }
}