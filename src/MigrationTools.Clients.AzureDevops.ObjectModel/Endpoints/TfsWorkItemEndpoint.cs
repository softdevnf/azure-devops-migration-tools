﻿using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using MigrationTools.DataContracts;
using MigrationTools.EndpointEnrichers;
using MigrationTools.Options;
using WorkItem = Microsoft.TeamFoundation.WorkItemTracking.Client.WorkItem;

namespace MigrationTools.Endpoints
{
    public class TfsWorkItemEndpoint : GenericTfsEndpoint<TfsWorkItemEndpointOptions>, IWorkItemSourceEndpoint, IWorkItemTargetEndpoint
    {
        public TfsWorkItemEndpoint(EndpointEnricherContainer endpointEnrichers, ITelemetryLogger telemetry, ILogger<TfsWorkItemEndpoint> logger)
            : base(endpointEnrichers, telemetry, logger)
        {
        }

        public override void Configure(TfsWorkItemEndpointOptions options)
        {
            base.Configure(options);
            Log.LogDebug("TfsWorkItemEndPoint::Configure");
            if (string.IsNullOrEmpty(Options.Query?.Query))
            {
                throw new ArgumentNullException(nameof(Options.Query));
            }
        }

        public void Filter(IEnumerable<WorkItemData> workItems)
        {
            Log.LogDebug("TfsWorkItemEndPoint::Filter");
        }

        public IEnumerable<WorkItemData> GetWorkItems()
        {
            Log.LogDebug("TfsWorkItemEndPoint::GetWorkItems");
            if (string.IsNullOrEmpty(Options.Query?.Query))
            {
                throw new ArgumentNullException(nameof(Options.Query));
            }
            return GetWorkItems(Options.Query);
        }

        public IEnumerable<WorkItemData> GetWorkItems(QueryOptions query)
        {
            Log.LogDebug("TfsWorkItemEndPoint::GetWorkItems(query)");
            var wis = TfsStore.Query(query.Query, query.Paramiters);
            return ToWorkItemDataList(wis);
        }

        private List<WorkItemData> ToWorkItemDataList(WorkItemCollection collection)
        {
            List<WorkItemData> list = new List<WorkItemData>();
            foreach (WorkItem wi in collection)
            {
                list.Add(ConvertToWorkItemData(wi));
            }
            return list;
        }

        private WorkItemData ConvertToWorkItemData(WorkItem wi)
        {
            WorkItemData wid = new WorkItemData
            {
                Id = wi.Id.ToString(),
                Type = wi.Type.ToString()
            };
            PopulateRevisions(wi, wid);
            return wid;
        }

        private void PopulateRevisions(WorkItem wi, WorkItemData wid)
        {
            wid.Revisions = new SortedDictionary<int, RevisionItem>();
            foreach (Revision revision in wi.Revisions)
            {
                RevisionItem revi = new RevisionItem
                {
                    Number = revision.Index,
                    Index = revision.Index
                };
                RunSourceEnrichers(revision, revi);
                wid.Revisions.Add(revision.Index, revi);
            }
        }

        private void RunSourceEnrichers(Revision wi, RevisionItem wid)
        {
            Log.LogDebug("TfsWorkItemEndPoint::RunSourceEnrichers::{SourceEnrichersCount}", SourceEnrichers.Count());
            foreach (IWorkItemEndpointSourceEnricher enricher in SourceEnrichers)
            {
                enricher.EnrichWorkItemData(this, wi, wid); // HELP:: is this Right
            }
        }

        public void PersistWorkItem(WorkItemData source)
        {
            Log.LogDebug("TfsWorkItemEndPoint::PersistWorkItem");
        }
    }
}