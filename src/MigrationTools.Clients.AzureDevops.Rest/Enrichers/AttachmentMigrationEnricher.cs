﻿using System;
using MigrationTools._EngineV1.DataContracts;
using MigrationTools._EngineV1.Enrichers;

namespace MigrationTools.Clients.AzureDevops.Rest.Enrichers
{
    public class AttachmentMigrationEnricher : IAttachmentMigrationEnricher
    {
        private string _exportBasePath;
        private int _maxAttachmentSize;

        public AttachmentMigrationEnricher(string exportBasePath, int maxAttachmentSize = 480000000)
        {
            _exportBasePath = exportBasePath;
            _maxAttachmentSize = maxAttachmentSize;
        }

        public void ProcessAttachemnts(WorkItemData source, WorkItemData target, bool save = true)
        {
            throw new NotImplementedException();
        }

        public void CleanUpAfterSave()
        {
            throw new NotImplementedException();
        }
    }
}