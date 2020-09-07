﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.TeamFoundation.Server;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using AzureDevOpsMigrationTools.Core.Configuration.Processing;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;
using System.Collections;
using VstsSyncMigrator.Core.Execution.OMatics;
using Microsoft.TeamFoundation.WorkItemTracking.Proxy;
using System.Net;
using System.ServiceModel.Channels;
using System.IO;
using Newtonsoft.Json;
using VstsSyncMigrator.Core;
using Serilog;
using Serilog.Events;
using Serilog.Context;
using Microsoft.ApplicationInsights.DataContracts;

namespace VstsSyncMigrator.Engine
{

    public class WorkItemMigrationContext : MigrationContextBase
    {
        private readonly WorkItemMigrationConfig _config;
        private List<String> _ignore;
        private WorkItemTrackingHttpClient _witClient;
        private WorkItemLinkOMatic workItemLinkOMatic = new WorkItemLinkOMatic();
        private AttachmentOMatic attachmentOMatic;
        private RepoOMatic repoOMatic;
        private ILogger contextLog;
       private ILogger workItemLog;
        EmbededImagesRepairOMatic embededImagesRepairOMatic = new EmbededImagesRepairOMatic();
        static int _current = 0;
        static int _count = 0;
        static int _failures = 0;
        static int _imported = 0;
        static int _skipped = 0;
        static long _elapsedms = 0;
        static int _totalWorkItem = 0;
        static string workItemLogTeamplate = "[{sourceWorkItemTypeName,20}][Complete:{currentWorkItem,6}/{totalWorkItems}][sid:{sourceWorkItemId,6}|Rev:{sourceRevisionInt,3}][tid:{targetWorkItemId,6} | ";

        public WorkItemMigrationContext(MigrationEngine me, WorkItemMigrationConfig config)
            : base(me, config)
        {
            contextLog = Log.ForContext<WorkItemMigrationContext>();
            _config = config;
            PopulateIgnoreList();

            VssClientCredentials adoCreds = new VssClientCredentials();
            _witClient = new WorkItemTrackingHttpClient(me.Target.Collection.Uri, adoCreds);

            var workItemServer = me.Source.Collection.GetService<WorkItemServer>();
            attachmentOMatic = new AttachmentOMatic(workItemServer, config.AttachmentWorkingPath, config.AttachmentMazSize);
            repoOMatic = new RepoOMatic(me);


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
                "Microsoft.VSTS.Common.StateChangeDate",
                "System.ExternalLinkCount",
                "System.HyperLinkCount",
                "System.Watermark",
                "System.AuthorizedDate",
                "System.BoardColumn",
                "System.BoardColumnDone",
                "System.BoardLane",
                "SLB.SWT.DateOfClientFeedback",
                "System.CommentCount",
                "System.RemoteLinkCount"
            };
        }

        public override string Name => "WorkItemMigration";

        internal override void InternalExecute()
        {
            var stopwatch = Stopwatch.StartNew();
            //////////////////////////////////////////////////
            var sourceStore = new WorkItemStoreContext(me.Source, WorkItemStoreFlags.BypassRules);
            var tfsqc = new TfsQueryContext(sourceStore);
            tfsqc.AddParameter("TeamProject", me.Source.Config.Project);
            tfsqc.Query =
                string.Format(
                    @"SELECT [System.Id], [System.Tags] FROM WorkItems WHERE [System.TeamProject] = @TeamProject {0} ORDER BY {1}",
                    _config.QueryBit, _config.OrderBit);
            var sourceQueryResult = tfsqc.Execute();
            var sourceWorkItems = (from WorkItem swi in sourceQueryResult select swi).ToList();
            contextLog.Information("Replay all revisions of {sourceWorkItemsCount} work items?", sourceWorkItems.Count);
            //////////////////////////////////////////////////
            var targetStore = new WorkItemStoreContext(me.Target, WorkItemStoreFlags.BypassRules);
            var destProject = targetStore.GetProject();
            contextLog.Information("Found target project as {@destProject}", destProject);
            //////////////////////////////////////////////////////////FilterCompletedByQuery
            if (_config.FilterWorkItemsThatAlreadyExistInTarget)
            {
                sourceWorkItems = FilterWorkItemsThatAlreadyExistInTarget(sourceWorkItems, targetStore);
            }
            //////////////////////////////////////////////////
            _current = 1;
            _count = sourceWorkItems.Count;
            _elapsedms = 0;
            _totalWorkItem = sourceWorkItems.Count;

            //Validation: make sure that the ReflectedWorkItemId field name specified in the config exists in the target process, preferably on each work item type.
            ConfigValidation();

            foreach (WorkItem sourceWorkItem in sourceWorkItems)
            {
                workItemLog = contextLog.ForContext("SourceWorkItemId", sourceWorkItem.Id);
                using (LogContext.PushProperty("sourceWorkItemTypeName", sourceWorkItem.Type.Name))
                using (LogContext.PushProperty("currentWorkItem", _current))
                using (LogContext.PushProperty("totalWorkItems", _totalWorkItem))
                using (LogContext.PushProperty("sourceWorkItemId", sourceWorkItem.Id))
                using (LogContext.PushProperty("sourceRevisionInt", sourceWorkItem.Revision))
                using (LogContext.PushProperty("targetWorkItemId", null))
                {
                    ProcessWorkItem(sourceStore, targetStore, destProject, sourceWorkItem, _config.WorkItemCreateRetryLimit);
                    if (_config.PauseAfterEachWorkItem)
                    {
                        Console.WriteLine("Do you want to continue? (y/n)");
                        if (Console.ReadKey().Key != ConsoleKey.Y)
                        {
                            workItemLog.Warning("USER ABORTED");
                            break;
                        }
                    }
                }               
            }
            //////////////////////////////////////////////////
            stopwatch.Stop();

            contextLog.Information("DONE in {Elapsed:%h} hours {Elapsed:%m} minutes {Elapsed:s/:fff} seconds",  stopwatch.Elapsed);
        }



        private IDictionary<string, double> processWorkItemMetrics = null;
        private IDictionary<string, string> processWorkItemParamiters = null;

        private void ProcessWorkItem(WorkItemStoreContext sourceStore, WorkItemStoreContext targetStore, Project destProject, WorkItem sourceWorkItem, int retryLimit = 5, int retrys = 0)
        {

            var witstopwatch = Stopwatch.StartNew();
            var starttime = DateTime.Now;
            processWorkItemMetrics = new Dictionary<string, double>();
            processWorkItemParamiters = new Dictionary<string, string>();
            AddParameter("SourceURL", processWorkItemParamiters, sourceStore.Store.TeamProjectCollection.Uri.ToString());
            AddParameter("SourceWorkItem", processWorkItemParamiters, sourceWorkItem.Id.ToString());
            AddParameter("TargetURL", processWorkItemParamiters, targetStore.Store.TeamProjectCollection.Uri.ToString());
            AddParameter("TargetProject", processWorkItemParamiters, destProject.Name);
            AddParameter("RetryLimit", processWorkItemParamiters, retryLimit.ToString());
            AddParameter("RetryNumber", processWorkItemParamiters, retrys.ToString());

            try
            {
                if (sourceWorkItem.Type.Name != "Test Plan" || sourceWorkItem.Type.Name != "Test Suite")
                {
                    var targetWorkItem = targetStore.FindReflectedWorkItem(sourceWorkItem, false);
                    ///////////////////////////////////////////////
                    TraceWriteLine(LogEventLevel.Information, "Work Item has {sourceWorkItemRev} revisions and revision migration is set to {ReplayRevisions}", sourceWorkItem.Rev, _config.ReplayRevisions);
                    List<RevisionItem> revisionsToMigrate = RevisionsToMigrate(sourceWorkItem, targetWorkItem);
                    if (targetWorkItem == null)
                    {
                        targetWorkItem = ReplayRevisions(revisionsToMigrate, sourceWorkItem, null, destProject, sourceStore, _current, targetStore);
                        AddMetric("Revisions", processWorkItemMetrics, revisionsToMigrate.Count);
                    }
                    else
                    {
                        if (revisionsToMigrate.Count == 0)
                        {
                            ProcessWorkItemAttachments(sourceWorkItem, targetWorkItem, false);
                            ProcessWorkItemLinks(sourceStore, targetStore, sourceWorkItem, targetWorkItem);
                            TraceWriteLine(LogEventLevel.Information, "Skipping as work item exists and no revisions to sync detected");
                            processWorkItemMetrics.Add("Revisions", 0);
                        }
                        else
                        {
                            TraceWriteLine(LogEventLevel.Information, "Syncing as there are {revisionsToMigrateCount} revisons detected", revisionsToMigrate.Count);

                            targetWorkItem = ReplayRevisions(revisionsToMigrate, sourceWorkItem, targetWorkItem, destProject, sourceStore, _current, targetStore);

                            AddMetric("Revisions", processWorkItemMetrics, revisionsToMigrate.Count);
                            AddMetric("SyncRev", processWorkItemMetrics, revisionsToMigrate.Count);
                        }


                    }
                    AddParameter("TargetWorkItem", processWorkItemParamiters, targetWorkItem.Revisions.Count.ToString());
                    ///////////////////////////////////////////////
                    ProcessHTMLFieldAttachements(targetWorkItem);
                    ///////////////////////////////////////////////
                    ///////////////////////////////////////////////////////
                    if (targetWorkItem != null && targetWorkItem.IsDirty)
                    {
                        SaveWorkItem(targetWorkItem);
                    }
                    if (targetWorkItem != null)
                    {
                        targetWorkItem.Close();
                    }
                    if (sourceWorkItem != null)
                    {
                        sourceWorkItem.Close();
                    }
                }
                else
                {
                    TraceWriteLine(LogEventLevel.Warning, "SKIP: Unable to migrate {sourceWorkItemTypeName}/{sourceWorkItemId}. Use the TestPlansAndSuitesMigrationContext after you have migrated all Test Cases. ", sourceWorkItem.Type.Name,sourceWorkItem.Id);
                }
            }
            catch (WebException ex)
            {

                Telemetry.Current.TrackException(ex);

                TraceWriteLine(LogEventLevel.Error, ex.ToString(), ex);
                if (retrys < retryLimit)
                {
                    TraceWriteLine(LogEventLevel.Warning, "WebException: Will retry in {retrys}s ", retrys);
                    System.Threading.Thread.Sleep(new TimeSpan(0, 0, retrys));
                    retrys++;
                    TraceWriteLine(LogEventLevel.Warning, "RETRY {retrys}/{retrys} ", retrys, retrys);
                    ProcessWorkItem(sourceStore, targetStore, destProject, sourceWorkItem, retryLimit, retrys);
                }
                else
                {
                    TraceWriteLine(LogEventLevel.Error, "ERROR: Failed to create work item. Retry Limit reached ");
                }
            }
            catch (Exception ex)
            {
                Telemetry.Current.TrackException(ex);
                TraceWriteLine(LogEventLevel.Error, ex.ToString(), ex);
                Telemetry.Current.TrackRequest("ProcessWorkItem", starttime, witstopwatch.Elapsed, "502", false);
                throw ex;
            }
            witstopwatch.Stop();
            _elapsedms += witstopwatch.ElapsedMilliseconds;
            processWorkItemMetrics.Add("ElapsedTimeMS", _elapsedms);


            var average = new TimeSpan(0, 0, 0, 0, (int)(_elapsedms / _current));
            var remaining = new TimeSpan(0, 0, 0, 0, (int)(average.TotalMilliseconds * _count));
            TraceWriteLine(LogEventLevel.Information, "Average time of {average:s/:fff} per work item and {remaining:%h} hours {remaining:%m} minutes {remaining:s/:fff} seconds estimated to completion", average, remaining);
            Trace.Flush();
            Telemetry.Current.TrackEvent("WorkItemMigrated", processWorkItemParamiters, processWorkItemMetrics);
            Telemetry.Current.TrackRequest("ProcessWorkItem", starttime, witstopwatch.Elapsed, "200", true);

            _current++;
            _count--;
        }


        private List<RevisionItem> RevisionsToMigrate(WorkItem sourceWorkItem, WorkItem targetWorkItem)
        {
            // just to make sure, we replay the events in the same order as they appeared
            // maybe, the Revisions collection is not sorted according to the actual Revision number
            List<RevisionItem> sortedRevisions = null;
            sortedRevisions = sourceWorkItem.Revisions.Cast<Revision>()
                    .Select(x => new RevisionItem
                    {
                        Index = x.Index,
                        Number = Convert.ToInt32(x.Fields["System.Rev"].Value),
                        ChangedDate = Convert.ToDateTime(x.Fields["System.ChangedDate"].Value)


                    })
                    .ToList();

            if (targetWorkItem != null)
            {
                // Target exists so remove any Changed Date matches bwtween them
                var targetChangedDates = (from Revision x in targetWorkItem.Revisions select Convert.ToDateTime(x.Fields["System.ChangedDate"].Value)).ToList();
                if (_config.ReplayRevisions)
                {
                    sortedRevisions = sortedRevisions.Where(x => !targetChangedDates.Contains(x.ChangedDate)).ToList();
                }
                // Find Max target date and remove all source revisions that are newer
                var targetLatestDate = targetChangedDates.Max();
                sortedRevisions = sortedRevisions.Where(x => x.ChangedDate > targetLatestDate).ToList();
            }

            sortedRevisions = sortedRevisions.OrderBy(x => x.Number).ToList();
            if (!_config.ReplayRevisions && sortedRevisions.Count > 0)
            {
                // Remove all but the latest revision if we are not replaying reviss=ions
                sortedRevisions.RemoveRange(0, sortedRevisions.Count - 1);
            }

            TraceWriteLine(LogEventLevel.Information, "Found {RevisionsCount} revisions to migrate on  Work item:{sourceWorkItemId}", sortedRevisions.Count, sourceWorkItem.Id);
            return sortedRevisions;
        }

        private class RevisionItem
        {
            public int Index { get; set; }
            public int Number { get; set; }
            public DateTime ChangedDate { get; internal set; }
        }

        private WorkItem ReplayRevisions(List<RevisionItem> revisionsToMigrate, WorkItem sourceWorkItem, WorkItem targetWorkItem, Project destProject, WorkItemStoreContext sourceStore,
            int current,
            WorkItemStoreContext targetStore)
        {

            try
            {
                var skipToFinalRevisedWorkItemType = _config.SkipToFinalRevisedWorkItemType;

                var last = sourceStore.GetRevision(sourceWorkItem, revisionsToMigrate.Last().Number);

                string finalDestType = last.Type.Name;

                if (skipToFinalRevisedWorkItemType && me.WorkItemTypeDefinitions.ContainsKey(finalDestType))
                {
                    finalDestType =
                       me.WorkItemTypeDefinitions[finalDestType].Map(last);
                }

                //If work item hasn't been created yet, create a shell
                if (targetWorkItem == null)
                {
                    targetWorkItem = CreateWorkItem_Shell(destProject, sourceWorkItem, skipToFinalRevisedWorkItemType ? finalDestType : sourceStore.GetRevision(sourceWorkItem, revisionsToMigrate.First().Number).Type.Name);
                }

                if (_config.CollapseRevisions)
                {
                    var data = revisionsToMigrate.Select(rev => sourceStore.GetRevision(sourceWorkItem, rev.Number)).Select(rev => new
                    {
                        rev.Id,
                        rev.Rev,
                        rev.RevisedDate,
                        Fields = rev.Fields.AsDictionary()
                    });

                    var fileData = JsonConvert.SerializeObject(data, new JsonSerializerSettings { PreserveReferencesHandling = PreserveReferencesHandling.None });
                    var filePath = Path.Combine(Path.GetTempPath(), $"{sourceWorkItem.Id}_PreMigrationHistory.json");

                    File.WriteAllText(filePath, fileData);
                    targetWorkItem.Attachments.Add(new Attachment(filePath, "History has been consolidated into the attached file."));

                    revisionsToMigrate = revisionsToMigrate.GetRange(revisionsToMigrate.Count - 1, 1);

                    TraceWriteLine( LogEventLevel.Information, " Attached a consolidated set of {RevisionCount} revisions.", data.Count());
                }

                foreach (var revision in revisionsToMigrate)
                {
                    var currentRevisionWorkItem = sourceStore.GetRevision(sourceWorkItem, revision.Number);

                    TraceWriteLine(LogEventLevel.Information, " Processing Revision [{RevisionNumber}]", revision.Number);

                    // Decide on WIT
                    string destType = currentRevisionWorkItem.Type.Name;
                    if (me.WorkItemTypeDefinitions.ContainsKey(destType))
                    {
                        destType =
                           me.WorkItemTypeDefinitions[destType].Map(currentRevisionWorkItem);
                    }

                    //If the work item already exists and its type has changed, update its type. Done this way because there doesn't appear to be a way to do this through the store.
                    if (!skipToFinalRevisedWorkItemType && targetWorkItem.Type.Name != finalDestType)

                    {
                        Debug.WriteLine($"Work Item type change! '{targetWorkItem.Title}': From {targetWorkItem.Type.Name} to {destType}");
                        var typePatch = new JsonPatchOperation()
                        {
                            Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                            Path = "/fields/System.WorkItemType",
                            Value = destType
                        };
                        var datePatch = new JsonPatchOperation()
                        {
                            Operation = Microsoft.VisualStudio.Services.WebApi.Patch.Operation.Add,
                            Path = "/fields/System.ChangedDate",
                            Value = currentRevisionWorkItem.Revisions[revision.Index].Fields["System.ChangedDate"].Value
                        };

                        var patchDoc = new JsonPatchDocument();
                        patchDoc.Add(typePatch);
                        patchDoc.Add(datePatch);
                        _witClient.UpdateWorkItemAsync(patchDoc, targetWorkItem.Id, bypassRules: true).Wait();
                    }

                    PopulateWorkItem(currentRevisionWorkItem, targetWorkItem, destType);
                    me.ApplyFieldMappings(currentRevisionWorkItem, targetWorkItem);

                    targetWorkItem.Fields["System.ChangedBy"].Value =
                        currentRevisionWorkItem.Revisions[revision.Index].Fields["System.ChangedBy"].Value;

                    targetWorkItem.Fields["System.History"].Value =
                        currentRevisionWorkItem.Revisions[revision.Index].Fields["System.History"].Value;
                    //Debug.WriteLine("Discussion:" + currentRevisionWorkItem.Revisions[revision.Index].Fields["System.History"].Value);


                    var fails = targetWorkItem.Validate();

                    foreach (Field f in fails)
                    {
                        TraceWriteLine( LogEventLevel.Information,
                            "{Current} - Invalid: {CurrentRevisionWorkItemId}-{CurrentRevisionWorkItemTypeName}-{FieldReferenceName}-{SourceWorkItemTitle} Value: {FieldValue}",
                            current,
                            currentRevisionWorkItem.Id,
                            currentRevisionWorkItem.Type.Name,
                            f.ReferenceName,
                            sourceWorkItem.Title,
                            f.Value);
                    }
                    //if (fails.Count > 0) // TODO Do we want to stop or continue?
                    //{
                    //    contextLog.Error("Unable to save revission due to the error in validation");
                    //    break;
                    //}

                    targetWorkItem.Save();
                    TraceWriteLine( LogEventLevel.Information,
                        " Saved TargetWorkItem {TargetWorkItemId}. Replayed revision {RevisionNumber} of {RevisionsToMigrateCount}", 
                        targetWorkItem.Id, 
                        revision.Number, 
                        revisionsToMigrate.Count);

                }

                if (targetWorkItem != null)
                {
                    ProcessWorkItemAttachments(sourceWorkItem, targetWorkItem, false);
                    ProcessWorkItemLinks(sourceStore, targetStore, sourceWorkItem, targetWorkItem);
                    string reflectedUri = sourceStore.CreateReflectedWorkItemId(sourceWorkItem);
                    if (targetWorkItem.Fields.Contains(me.Target.Config.ReflectedWorkItemIDFieldName))
                    {

                        targetWorkItem.Fields[me.Target.Config.ReflectedWorkItemIDFieldName].Value = reflectedUri;
                    }
                    var history = new StringBuilder();
                    history.Append(
                        $"This work item was migrated from a different project or organization. You can find the old version at <a href=\"{reflectedUri}\">{reflectedUri}</a>.");
                    targetWorkItem.History = history.ToString();
                    SaveWorkItem(targetWorkItem);

                    attachmentOMatic.CleanUpAfterSave(targetWorkItem);
                    TraceWriteLine (LogEventLevel.Information, "...Saved as {TargetWorkItemId}", targetWorkItem.Id);
                }
            }
            catch (Exception ex)
            {
                TraceWriteLine(LogEventLevel.Information, "...FAILED to Save");

                if (targetWorkItem != null)
                {
                    foreach (Field f in targetWorkItem.Fields)
                        TraceWriteLine(LogEventLevel.Information, "{FieldReferenceName} ({FieldName}) | {FieldValue}", f.ReferenceName, f.Name, f.Value);
                }
                TraceWriteLine(LogEventLevel.Information, ex.ToString(), ex);
            }

            return targetWorkItem;
        }

        private WorkItem CreateWorkItem_Shell(Project destProject, WorkItem currentRevisionWorkItem, string destType)
        {
            WorkItem newwit;
            var newWorkItemstartTime = DateTime.UtcNow;
            var newWorkItemTimer = Stopwatch.StartNew();
            if (destProject.WorkItemTypes.Contains(destType))
            {
                newwit = destProject.WorkItemTypes[destType].NewWorkItem();
            }
            else
            {
                throw new Exception(string.Format("WARNING: Unable to find '{0}' in the target project. Most likley this is due to a typo in the .json configuration under WorkItemTypeDefinition! ", destType));
            }
            newWorkItemTimer.Stop();
            Telemetry.Current.TrackDependency(new DependencyTelemetry("TeamService", "NewWorkItem", newWorkItemstartTime, newWorkItemTimer.Elapsed, true));
            if (_config.UpdateCreatedBy) { newwit.Fields["System.CreatedBy"].Value = currentRevisionWorkItem.Revisions[0].Fields["System.CreatedBy"].Value; }
            if (_config.UpdateCreatedDate) { newwit.Fields["System.CreatedDate"].Value = currentRevisionWorkItem.Revisions[0].Fields["System.CreatedDate"].Value; }

            return newwit;
        }

        private void PopulateWorkItem(WorkItem oldWi, WorkItem newwit, string destType)
        {
            var newWorkItemstartTime = DateTime.UtcNow;
            var fieldMappingTimer = Stopwatch.StartNew();

            if (newwit.IsPartialOpen || !newwit.IsOpen)
            {
                newwit.Open();
            }

            newwit.Title = oldWi.Title;
            newwit.State = oldWi.State;
            newwit.Reason = oldWi.Reason;

            foreach (Field f in oldWi.Fields)
            {
                if (newwit.Fields.Contains(f.ReferenceName) && !_ignore.Contains(f.ReferenceName) && (!newwit.Fields[f.ReferenceName].IsChangedInRevision || newwit.Fields[f.ReferenceName].IsEditable))
                {
                    newwit.Fields[f.ReferenceName].Value = oldWi.Fields[f.ReferenceName].Value;
                }
            }

            newwit.AreaPath = GetNewNodeName(oldWi.AreaPath, oldWi.Project.Name, newwit.Project.Name, newwit.Store, me.Target.Config.LanguageMaps.AreaPath);
            newwit.IterationPath = GetNewNodeName(oldWi.IterationPath, oldWi.Project.Name, newwit.Project.Name, newwit.Store, me.Target.Config.LanguageMaps.IterationPath);

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
            fieldMappingTimer.Stop();
            // Trace.WriteLine(
            //    $"FieldMapOnNewWorkItem: {newWorkItemstartTime} - {fieldMappingTimer.Elapsed.ToString("c")}", Name);
        }

        internal void TraceWriteLine(LogEventLevel level, string message = "", params object[] propertyValues)
        {
            workItemLog.Write(level, workItemLogTeamplate + message,propertyValues);
        }

        private List<WorkItem> FilterWorkItemsThatAlreadyExistInTarget(List<WorkItem> sourceWorkItems, WorkItemStoreContext targetStore)
        {
            var targetQuery = new TfsQueryContext(targetStore);
            targetQuery.AddParameter("TeamProject", me.Target.Config.Project);
            targetQuery.Query =
                string.Format(
                    @"SELECT [System.Id], [{0}] FROM WorkItems WHERE [System.TeamProject] = @TeamProject {1} ORDER BY {2}",
                     me.Target.Config.ReflectedWorkItemIDFieldName,
                    _config.QueryBit,
                    _config.OrderBit
                    );
            var targetFoundItems = targetQuery.Execute();
            var targetFoundIds = (from WorkItem twi in targetFoundItems select targetStore.GetReflectedWorkItemId(twi, me.Target.Config.ReflectedWorkItemIDFieldName)).ToList();
            //////////////////////////////////////////////////////////

            sourceWorkItems = sourceWorkItems.Where(p => !targetFoundIds.Any(p2 => p2 == p.Id)).ToList();
            return sourceWorkItems;
        }

        NodeDetecomatic _nodeOMatic;

        private string GetNewNodeName(string oldNodeName, string oldProjectName, string newProjectName, WorkItemStore newStore, string nodePath)
        {
            if (_nodeOMatic == null)
            {
                _nodeOMatic = new NodeDetecomatic(newStore);
            }

            // Replace project name with new name (if necessary) and inject nodePath (Area or Iteration) into path for node validation
            string newNodeName = "";
            if (_config.PrefixProjectToNodes)
            {
                newNodeName = $@"{newProjectName}\{nodePath}\{oldNodeName}";
            }
            else
            {
                var regex = new Regex(Regex.Escape(oldProjectName));
                if (oldNodeName.StartsWith($@"{oldProjectName}\{nodePath}\"))
                {
                    newNodeName = regex.Replace(oldNodeName, newProjectName, 1);
                }
                else
                {
                    newNodeName = regex.Replace(oldNodeName, $@"{newProjectName}\{nodePath}", 1);
                }
            }

            // Validate the node exists
            if (!_nodeOMatic.NodeExists(newNodeName))
            {
                contextLog.Warning("The Node '{newNodeName}' does not exist, leaving as '{newProjectName}'. This may be because it has been renamed or moved and no longer exists, or that you have not migrateed the Node Structure yet.", newNodeName, newProjectName);
                newNodeName = newProjectName;
            }

            // Remove nodePath (Area or Iteration) from path for correct population in work item
            if (newNodeName.StartsWith(newProjectName + '\\' + nodePath + '\\'))
            {
                return newNodeName.Remove(newNodeName.IndexOf($@"{nodePath}\"), $@"{nodePath}\".Length);
            }
            else if (newNodeName.StartsWith(newProjectName + '\\' + nodePath))
            {
                return newNodeName.Remove(newNodeName.IndexOf($@"{nodePath}"), $@"{nodePath}".Length);
            }
            else
            {
                return newNodeName;
            }
        }


        private static bool IsNumeric(string val, NumberStyles numberStyle)
        {
            double result;
            return double.TryParse(val, numberStyle,
                CultureInfo.CurrentCulture, out result);
        }

        private static void AppendMigratedByFooter(StringBuilder history)
        {
            history.Append("<p>Migrated by <a href='https://dev.azure.com/nkdagility/migration-tools/'>Azure DevOps Migration Tools</a> open source.</p>");
        }

        private static void BuildFieldTable(WorkItem oldWi, StringBuilder history, bool useHTML = false)
        {
            history.Append("<p>Fields from previous Work Item:</p>");
            foreach (Field f in oldWi.Fields)
            {
                if (f.Value == null)
                {
                    history.AppendLine(string.Format("{0}: null<br />", f.Name));
                }
                else
                {
                    history.AppendLine(string.Format("{0}: {1}<br />", f.Name, f.Value.ToString()));
                }

            }
            history.Append("<p>&nbsp;</p>");
        }

        private static void BuildCommentTable(WorkItem oldWi, StringBuilder history)
        {
            if (oldWi.Revisions != null && oldWi.Revisions.Count > 0)
            {
                history.Append("<p>Comments from previous work item:</p>");
                history.Append("<table border='1' style='width:100%;border-color:#C0C0C0;'>");
                foreach (Revision r in oldWi.Revisions)
                {
                    if ((string)r.Fields["System.History"].Value != "" && (string)r.Fields["System.ChangedBy"].Value != "Martin Hinshelwood (Adm)")
                    {
                        r.WorkItem.Open();
                        history.AppendFormat("<tr><td style='align:right;width:100%'><p><b>{0} on {1}:</b></p><p>{2}</p></td></tr>", r.Fields["System.ChangedBy"].Value, DateTime.Parse(r.Fields["System.ChangedDate"].Value.ToString()).ToLongDateString(), r.Fields["System.History"].Value);
                    }
                }
                history.Append("</table>");
                history.Append("<p>&nbsp;</p>");
            }
        }


        private void ProcessHTMLFieldAttachements(WorkItem targetWorkItem)
        {
            if (targetWorkItem != null && _config.FixHtmlAttachmentLinks)
            {

                embededImagesRepairOMatic.FixHtmlAttachmentLinks(targetWorkItem, me.Source.Collection.Uri.ToString(), me.Target.Collection.Uri.ToString(), me.Source.Config.PersonalAccessToken);
            }
        }

        private void ProcessWorkItemLinks(WorkItemStoreContext sourceStore, WorkItemStoreContext targetStore, WorkItem sourceWorkItem, WorkItem targetWorkItem)
        {
            if (targetWorkItem != null && _config.LinkMigration && sourceWorkItem.Links.Count > 0)
            {
                TraceWriteLine(LogEventLevel.Information, "Links {SourceWorkItemLinkCount} | LinkMigrator:{LinkMigration}", sourceWorkItem.Links.Count,_config.LinkMigration);
                workItemLinkOMatic.MigrateLinks(sourceWorkItem, sourceStore, targetWorkItem, targetStore, _config.LinkMigrationSaveEachAsAdded);
                AddMetric("RelatedLinkCount", processWorkItemMetrics, targetWorkItem.Links.Count);
                int fixedLinkCount = repoOMatic.FixExternalLinks(targetWorkItem, targetStore, sourceWorkItem, _config.LinkMigrationSaveEachAsAdded);
                AddMetric("FixedGitLinkCount", processWorkItemMetrics, fixedLinkCount);
            }
        }

        private void ProcessWorkItemAttachments(WorkItem sourceWorkItem, WorkItem targetWorkItem, bool save = true)
        {
            if (targetWorkItem != null && _config.AttachmentMigration && sourceWorkItem.Attachments.Count > 0)
            {
                TraceWriteLine(LogEventLevel.Information, "Attachemnts {SourceWorkItemAttachmentCount} | LinkMigrator:{AttachmentMigration}", sourceWorkItem.Attachments.Count, _config.AttachmentMigration);
                attachmentOMatic.ProcessAttachemnts(sourceWorkItem, targetWorkItem, save);
                AddMetric("Attachments", processWorkItemMetrics, targetWorkItem.AttachedFileCount);
            }
        }

        /// <summary>
        /// Validate the current configuration of the both the migrator and the target project
        /// </summary>
        private void ConfigValidation()
        {
            //Make sure that the ReflectedWorkItemId field name specified in the config exists in the target process, preferably on each work item type
            var fields = _witClient.GetFieldsAsync(me.Target.Config.Project).Result;
            bool rwiidFieldExists = fields.Any(x => x.ReferenceName == me.Target.Config.ReflectedWorkItemIDFieldName || x.Name == me.Target.Config.ReflectedWorkItemIDFieldName);
            Debug.WriteLine($"Found {fields.Count.ToString("n0")} work item fields.");
            if (rwiidFieldExists)
                Trace.WriteLine($"Found '{me.Target.Config.ReflectedWorkItemIDFieldName}' in this project, proceeding.");
            else
            {
                Trace.WriteLine($"Config file specifies '{me.Target.Config.ReflectedWorkItemIDFieldName}', which wasn't found.");
                Trace.WriteLine("Instead, found:");
                foreach (var field in fields.OrderBy(x => x.Name))
                    Trace.WriteLine($"{field.Type.ToString().PadLeft(15)} - {field.Name.PadRight(20)} - {field.ReferenceName ?? ""}");
                throw new Exception("Running a replay migration requires a ReflectedWorkItemId field to be defined in the target project's process.");
            }
        }



    }



    public class NodeDetecomatic
    {
        ICommonStructureService _commonStructure;
        List<string> _foundNodes = new List<string>();
        WorkItemStore _store;

        public NodeDetecomatic(WorkItemStore store)
        {
            _store = store;
            if (_commonStructure == null)
            {
                _commonStructure = (ICommonStructureService4)store.TeamProjectCollection.GetService(typeof(ICommonStructureService4));
            }
        }

        public bool NodeExists(string nodePath)
        {
            if (!_foundNodes.Contains(nodePath))
            {
                NodeInfo node = null;
                try
                {
                    node = _commonStructure.GetNodeFromPath(nodePath);
                }
                catch
                {
                    return false;
                }
                _foundNodes.Add(nodePath);
            }
            return true;
        }


    }
}
