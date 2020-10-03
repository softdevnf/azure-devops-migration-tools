﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MigrationTools;
using MigrationTools.Clients.AzureDevops.ObjectModel;
using MigrationTools.Clients.AzureDevops.ObjectModel.Enrichers;
using MigrationTools.Configuration;
using MigrationTools.Configuration.Processing;
using MigrationTools.DataContracts;

namespace VstsSyncMigrator.Engine
{
    public class FixGitCommitLinks : StaticProcessorBase
    {
        private FixGitCommitLinksConfig _config;
        private GitRepositoryEnricher _GitRepositoryEnricher;

        public FixGitCommitLinks(IServiceProvider services, IMigrationEngine me, ITelemetryLogger telemetry, ILogger<FixGitCommitLinks> logger) : base(services, me, telemetry, logger)
        {
            Logger = logger;
        }

        public override string Name
        {
            get
            {
                return "FixGitCommitLinks";
            }
        }

        public ILogger<FixGitCommitLinks> Logger { get; }

        public override void Configure(IProcessorConfig config)
        {
            _config = (FixGitCommitLinksConfig)config;
            _GitRepositoryEnricher = Services.GetRequiredService<GitRepositoryEnricher>();
        }

        protected override void InternalExecute()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            //////////////////////////////////////////////////
            var query =
                string.Format(
                    @"SELECT [System.Id] FROM WorkItems WHERE [System.TeamProject] = @TeamProject {0} ORDER BY {1}",
                    _config.QueryBit,
                    _config.OrderBit
                    );
            List<WorkItemData> workitems = Engine.Target.WorkItems.GetWorkItems(query);
            Trace.WriteLine(string.Format("Update {0} work items?", workitems.Count));
            //////////////////////////////////////////////////
            int current = workitems.Count;
            int count = 0;
            long elapsedms = 0;
            int noteFound = 0;
            foreach (WorkItemData workitem in workitems)
            {

                Stopwatch witstopwatch = Stopwatch.StartNew();
                workitem.ToWorkItem().Open();

                _GitRepositoryEnricher.Enrich(null, workitem);

                if (workitem.ToWorkItem().IsDirty)
                {
                    Trace.WriteLine($"Saving {workitem.Id}");

                    workitem.SaveToAzureDevOps();
                }

                witstopwatch.Stop();
                elapsedms = elapsedms + witstopwatch.ElapsedMilliseconds;
                current--;
                count++;
                TimeSpan average = new TimeSpan(0, 0, 0, 0, (int)(elapsedms / count));
                TimeSpan remaining = new TimeSpan(0, 0, 0, 0, (int)(average.TotalMilliseconds * current));
                Trace.WriteLine(string.Format("Average time of {0} per work item and {1} estimated to completion",
                    string.Format(@"{0:s\:fff} seconds", average),
                    string.Format(@"{0:%h} hours {0:%m} minutes {0:s\:fff} seconds", remaining)));

            }
            Trace.WriteLine(string.Format("Did not find old repo for {0} links?", noteFound));
            //////////////////////////////////////////////////
            stopwatch.Stop();
            Console.WriteLine(@"DONE in {0:%h} hours {0:%m} minutes {0:s\:fff} seconds", stopwatch.Elapsed);
        }

    }
}