﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

using Microsoft.TeamFoundation.WorkItemTracking.Client;

using VstsSyncMigrator.Engine.Configuration.Processing;

namespace VstsSyncMigrator.Engine
{
    public class WorkItemRevisionReplayMigrationContext : MigrationContextBase
    {
        private readonly WorkItemRevisionReplayMigrationConfig _config;
        List<String> _ignore;

        public WorkItemRevisionReplayMigrationContext(MigrationEngine me, WorkItemRevisionReplayMigrationConfig config)
            : base(me, config)
        {
            _config = config;
            PopulateIgnoreList();
        }

        private void PopulateIgnoreList()
        {
            _ignore = new List<string>
            {
                "System.Rev",
                "System.AreaId",
                "System.IterationId",
                "System.Id",
                "System.RevisedDate",
                "System.AuthorizedAs",
                "System.AttachedFileCount",
                "System.TeamProject",
                "System.NodeName",
                "System.RelatedLinkCount",
                "System.WorkItemType",
                "Microsoft.VSTS.Common.ActivatedDate",
                "Microsoft.VSTS.Common.ActivatedBy",
                "Microsoft.VSTS.Common.ResolvedDate",
                "Microsoft.VSTS.Common.ResolvedBy",
                "Microsoft.VSTS.Common.ClosedDate",
                "Microsoft.VSTS.Common.ClosedBy",
                "Microsoft.VSTS.Common.StateChangeDate",
                "System.ExternalLinkCount",
                "System.HyperLinkCount",
                "System.Watermark",
                "System.AuthorizedDate",
                "System.BoardColumn",
                "System.BoardColumnDone",
                "System.BoardLane",
                "SLB.SWT.DateOfClientFeedback"
            };
        }

        public override string Name => "WorkItemRevisionReplayMigrationContext";

        internal override void InternalExecute()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            //////////////////////////////////////////////////
            var sourceStore = new WorkItemStoreContext(me.Source, WorkItemStoreFlags.BypassRules);
            var tfsqc = new TfsQueryContext(sourceStore);
            tfsqc.AddParameter("TeamProject", me.Source.Name);
            tfsqc.Query =
                string.Format(
                    @"SELECT [System.Id], [System.Tags] FROM WorkItems WHERE [System.TeamProject] = @TeamProject {0} ORDER BY [System.ChangedDate] desc",
                    _config.QueryBit);
            var sourceWorkItems = tfsqc.Execute();
            Trace.WriteLine($"Replay all revisions of {sourceWorkItems.Count} work items?", Name);

            //////////////////////////////////////////////////
            var targetStore = new WorkItemStoreContext(me.Target, WorkItemStoreFlags.BypassRules);
            var destProject = targetStore.GetProject();
            Trace.WriteLine($"Found target project as {destProject.Name}", Name);

            var current = sourceWorkItems.Count;
            var count = 0;
            long elapsedms = 0;

            foreach (WorkItem sourceWorkItem in sourceWorkItems)
            {
                var witstopwatch = new Stopwatch();
                witstopwatch.Start();
                var targetFound = targetStore.FindReflectedWorkItem(sourceWorkItem, me.ReflectedWorkItemIdFieldName, false);
                Trace.WriteLine($"{current} - Migrating: {sourceWorkItem.Id} - {sourceWorkItem.Type.Name}", Name);

                if (targetFound == null)
                {
                    ReplayRevisions(sourceWorkItem, destProject, sourceStore, current, targetStore);
                }
                else
                {
                    Console.WriteLine("...Exists");
                }

                sourceWorkItem.Close();
                witstopwatch.Stop();
                elapsedms = elapsedms + witstopwatch.ElapsedMilliseconds;
                current--;
                count++;
                var average = new TimeSpan(0, 0, 0, 0, (int) (elapsedms / count));
                var remaining = new TimeSpan(0, 0, 0, 0, (int) (average.TotalMilliseconds * current));
                Trace.WriteLine(
                    string.Format("Average time of {0} per work item and {1} estimated to completion",
                        string.Format(@"{0:s\:fff} seconds", average),
                        string.Format(@"{0:%h} hours {0:%m} minutes {0:s\:fff} seconds", remaining)), Name);
                Trace.Flush();
            }
            //////////////////////////////////////////////////
            stopwatch.Stop();

            Console.WriteLine(@"DONE in {0:%h} hours {0:%m} minutes {0:s\:fff} seconds", stopwatch.Elapsed);
        }

        private void ReplayRevisions(WorkItem sourceWorkItem, Project destProject, WorkItemStoreContext sourceStore,
            int current,
            WorkItemStoreContext targetStore)
        {
            WorkItem newwit = null;

            try
            {
                // just to make sure, we replay the events in the same order as they appeared
                // maybe, the Revisions collection is not sorted according to the actual Revision number
                var sortedRevisions = sourceWorkItem.Revisions.Cast<Revision>().Select(x =>
                        new
                        {
                            x.Index,
                            Number =  Convert.ToInt32(x.Fields["System.Rev"].Value)
                        }
                    )
                    .OrderBy(x => x.Number)
                    .ToList();

                Trace.WriteLine($"...Replaying {sourceWorkItem.Revisions.Count} revisions of work item {sourceWorkItem.Id}", Name);

                foreach (var revision in sortedRevisions)
                {
                    var currentRevisionWorkItem = sourceStore.GetRevision(sourceWorkItem, revision.Number);

                    // Decide on WIT
                    if (me.WorkItemTypeDefinitions.ContainsKey(currentRevisionWorkItem.Type.Name))
                    {
                        var destType =
                            me.WorkItemTypeDefinitions[currentRevisionWorkItem.Type.Name].Map(currentRevisionWorkItem);

                        if (newwit == null)
                        {
                            var newWorkItemstartTime = DateTime.UtcNow;
                            var newWorkItemTimer = new Stopwatch();
                            newwit = destProject.WorkItemTypes[destType].NewWorkItem();
                            newWorkItemTimer.Stop();
                            Telemetry.Current.TrackDependency("TeamService", "NewWorkItem", newWorkItemstartTime,
                                newWorkItemTimer.Elapsed, true);
                            Trace.WriteLine(
                                string.Format("Dependency: {0} - {1} - {2} - {3} - {4}", "TeamService", "NewWorkItem",
                                    newWorkItemstartTime, newWorkItemTimer.Elapsed, true), Name);

                            newwit.Fields["System.CreatedBy"].Value = currentRevisionWorkItem.Revisions[0].Fields["System.CreatedBy"].Value;
                            newwit.Fields["System.CreatedDate"].Value = currentRevisionWorkItem.Revisions[0].Fields["System.CreatedDate"].Value;
                        }

                        PopulateWorkItem(currentRevisionWorkItem, newwit, destType);
                        me.ApplyFieldMappings(currentRevisionWorkItem, newwit);
                        var fails = newwit.Validate();

                        foreach (Field f in fails)
                        {
                            Trace.WriteLine(
                                $"{current} - Invalid: {currentRevisionWorkItem.Id}-{currentRevisionWorkItem.Type.Name}-{f.ReferenceName}",
                                Name);
                        }

                        newwit.Fields["System.ChangedBy"].Value = 
                            currentRevisionWorkItem.Revisions[revision.Index].Fields["System.ChangedBy"].Value;

                        newwit.Save();
                        Trace.WriteLine(
                            $" ...Saved as {newwit.Id}. Replayed revision {revision.Number} of {sourceWorkItem.Revisions.Count}",
                            Name);
                    }
                    else
                    {
                        Trace.WriteLine("...not supported", Name);
                        break;
                    }
                }

                if (newwit != null)
                {
                    if (newwit.Fields.Contains(me.ReflectedWorkItemIdFieldName))
                    {
                        newwit.Fields[me.ReflectedWorkItemIdFieldName].Value =
                            sourceStore.CreateReflectedWorkItemId(sourceWorkItem);
                    }

                    var history = new StringBuilder();
                    history.Append(
                        "Migrated by <a href='http://nkdagility.com'>naked Agility Limited's</a> open source <a href='https://github.com/nkdAgility/VstsMigrator'>VSTS/TFS Migrator</a>.");
                    newwit.History = history.ToString();

                    newwit.Save();
                    newwit.Close();
                    Trace.WriteLine($"...Saved as {newwit.Id}", Name);

                    if (sourceWorkItem.Fields.Contains(me.ReflectedWorkItemIdFieldName) &&
                        _config.UpdateSoureReflectedId)
                    {
                        sourceWorkItem.Fields[me.ReflectedWorkItemIdFieldName].Value =
                            targetStore.CreateReflectedWorkItemId(newwit);
                        sourceWorkItem.Save();
                        Trace.WriteLine($"...and Source Updated {sourceWorkItem.Id}", Name);
                    }

                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine("...FAILED to Save", Name);

                if (newwit != null)
                {
                    foreach (Field f in newwit.Fields)
                        Trace.WriteLine($"{f.ReferenceName} ({f.Name}) | {f.Value}", Name);
                }
                Trace.WriteLine(ex.ToString(), Name);
            }
        }

        private void PopulateWorkItem(WorkItem oldWi, WorkItem newwit, string destType)
        {
            var newWorkItemstartTime = DateTime.UtcNow;
            var fieldMappingTimer = new Stopwatch();
            
            Trace.Write("... Building ", Name);
            
            newwit.Title = oldWi.Title;
            newwit.State = oldWi.State;
            newwit.Reason = oldWi.Reason;

            foreach (Field f in oldWi.Fields)
            {
                if (newwit.Fields.Contains(f.ReferenceName) && !_ignore.Contains(f.ReferenceName))
                {
                    newwit.Fields[f.ReferenceName].Value = oldWi.Fields[f.ReferenceName].Value;
                }
            }

            if (_config.PrefixProjectToNodes)
            {
                newwit.AreaPath = $@"{newwit.Project.Name}\{oldWi.AreaPath}";
                newwit.IterationPath = $@"{newwit.Project.Name}\{oldWi.IterationPath}";
            }
            else
            {
                var regex = new Regex(Regex.Escape(oldWi.Project.Name));
                newwit.AreaPath = regex.Replace(oldWi.AreaPath, newwit.Project.Name, 1);
                newwit.IterationPath = regex.Replace(oldWi.IterationPath, newwit.Project.Name, 1);
            }

            switch (destType)
            {
                case "Test Case":
                    newwit.Fields["Microsoft.VSTS.TCM.Steps"].Value = oldWi.Fields["Microsoft.VSTS.TCM.Steps"].Value;
                    newwit.Fields["Microsoft.VSTS.Common.Priority"].Value =
                        oldWi.Fields["Microsoft.VSTS.Common.Priority"].Value;
                    break;
            }
            
            if (newwit.Fields.Contains("Microsoft.VSTS.Common.BacklogPriority")
                && newwit.Fields["Microsoft.VSTS.Common.BacklogPriority"].Value != null
                && !IsNumeric(newwit.Fields["Microsoft.VSTS.Common.BacklogPriority"].Value.ToString(),
                    NumberStyles.Any))
                newwit.Fields["Microsoft.VSTS.Common.BacklogPriority"].Value = 10;

            var description = new StringBuilder();
            description.Append(oldWi.Description);
            newwit.Description = description.ToString();

            Trace.WriteLine("...build complete", Name);
            fieldMappingTimer.Stop();
            Telemetry.Current.TrackMetric("FieldMappingTime", fieldMappingTimer.ElapsedMilliseconds);
            Trace.WriteLine(
                $"FieldMapOnNewWorkItem: {newWorkItemstartTime} - {fieldMappingTimer.Elapsed.ToString("c")}", Name);
        }
        
        private static bool IsNumeric(string val, NumberStyles numberStyle)
        {
            double result;
            return double.TryParse(val, numberStyle,
                CultureInfo.CurrentCulture, out result);
        }
    }
}