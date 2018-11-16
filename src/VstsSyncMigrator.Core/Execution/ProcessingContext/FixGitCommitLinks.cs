﻿using Microsoft.TeamFoundation.WorkItemTracking.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Linq;
using Microsoft.TeamFoundation.Git.Client;
using Microsoft.TeamFoundation;

using VstsSyncMigrator.Engine.Configuration.Processing;
using Microsoft.TeamFoundation.SourceControl.WebApi;

namespace VstsSyncMigrator.Engine
{
    public class FixGitCommitLinks : ProcessingContextBase
    {
        private FixGitCommitLinksConfig _config;

        public FixGitCommitLinks(MigrationEngine me, FixGitCommitLinksConfig config ) : base(me, config)
        {
            _config = config;
        }

        public override string Name
        {
            get
            {
                return "FixGitCommitLinks";
            }
        }

        internal override void InternalExecute()
        {
            Stopwatch stopwatch = new Stopwatch();
            stopwatch.Start();
            //////////////////////////////////////////////////
            var sourceGitRepoService = me.Source.Collection.GetService<GitRepositoryService>();
            var sourceGitRepos = sourceGitRepoService.QueryRepositories(me.Source.Name);
            //////////////////////////////////////////////////
            var targetGitRepoService = me.Target.Collection.GetService<GitRepositoryService>();
            var targetGitRepos = targetGitRepoService.QueryRepositories(me.Target.Name);

            WorkItemStoreContext targetStore = new WorkItemStoreContext(me.Target, WorkItemStoreFlags.BypassRules);
            TfsQueryContext tfsqc = new TfsQueryContext(targetStore);
            tfsqc.AddParameter("TeamProject", me.Target.Name);
            tfsqc.Query = string.Format(@"SELECT [System.Id] FROM WorkItems WHERE  [System.TeamProject] = @TeamProject");
            WorkItemCollection workitems = tfsqc.Execute();
            Trace.WriteLine(string.Format("Update {0} work items?", workitems.Count));
            //////////////////////////////////////////////////
            int current = workitems.Count;
            int count = 0;
            long elapsedms = 0;
            int noteFound = 0;
            foreach (WorkItem workitem in workitems)
            {
                Stopwatch witstopwatch = new Stopwatch();
                witstopwatch.Start();
                workitem.Open();
                List<ExternalLink> newEL = new List<ExternalLink>();
                List<ExternalLink> removeEL = new List<ExternalLink>();
                Trace.WriteLine(string.Format("WI: {0}?", workitem.Id));
                List<string> gitWits = new List<string>
                {
                    "Branch",
                    "Fixed in Commit",
                    "Pull Request"
                };

                foreach (Link l in workitem.Links)
                {
                    if (l is ExternalLink && gitWits.Contains(l.ArtifactLinkType.Name))
                    {
                        ExternalLink el = (ExternalLink) l;
                        //vstfs:///Git/Commit/25f94570-e3e7-4b79-ad19-4b434787fd5a%2f50477259-3058-4dff-ba4c-e8c179ec5327%2f41dd2754058348d72a6417c0615c2543b9b55535
                        string guidbits = el.LinkedArtifactUri.Substring(el.LinkedArtifactUri.LastIndexOf('/') + 1);
                        string[] bits = Regex.Split(guidbits, "%2f", RegexOptions.IgnoreCase);
                        string oldCommitId = null;
                        string oldGitRepoId = bits[1];
                        if (bits.Count() >= 3)
                        {
                            oldCommitId = $"{bits[2]}";
                            for (int i = 3; i < bits.Count(); i++)
                            {
                                oldCommitId += $"%2f{bits[i]}";
                            }
                        } else
                        {
                            oldCommitId = bits[2];
                        }
                        var oldGitRepo =
                            (from g in sourceGitRepos where g.Id.ToString() == oldGitRepoId select g)
                            .SingleOrDefault();

                        if(oldGitRepo != null)
                        {
                            // Find the target git repo
                            GitRepository newGitRepo = null;
                            var repoNameToLookFor = !string.IsNullOrEmpty(_config.TargetRepository)
                                ? _config.TargetRepository
                                : oldGitRepo.Name;

                            // Source and Target project names match
                            if (oldGitRepo.ProjectReference.Name == me.Target.Name)
                            {
                                newGitRepo = (from g in targetGitRepos
                                                  where
                                                  g.Name == repoNameToLookFor &&
                                                  g.ProjectReference.Name == oldGitRepo.ProjectReference.Name
                                                  select g).SingleOrDefault();
                            }
                            // Source and Target project names do not match
                            else
                            {
                                newGitRepo = (from g in targetGitRepos
                                              where
                                              g.Name == repoNameToLookFor &&
                                              g.ProjectReference.Name != oldGitRepo.ProjectReference.Name
                                              select g).SingleOrDefault();
                            }

                            // Fix commit links if target repo has been found
                            if (newGitRepo != null)
                            {
                                Trace.WriteLine($"Fixing {oldGitRepo.RemoteUrl} to {newGitRepo.RemoteUrl}?");

                                // Create External Link object
                                ExternalLink newLink = null;
                                switch(l.ArtifactLinkType.Name)
                                {
                                    case "Branch":
                                        newLink = new ExternalLink(targetStore.Store.RegisteredLinkTypes[ArtifactLinkIds.Branch],
                                            $"vstfs:///git/ref/{newGitRepo.ProjectReference.Id}%2f{newGitRepo.Id}%2f{oldCommitId}");
                                        break;

                                    case "Fixed in Commit":
                                        newLink = new ExternalLink(targetStore.Store.RegisteredLinkTypes[ArtifactLinkIds.Commit],
                                            $"vstfs:///git/commit/{newGitRepo.ProjectReference.Id}%2f{newGitRepo.Id}%2f{oldCommitId}");
                                        break;
                                    case "Pull Request":
                                        newLink = new ExternalLink(targetStore.Store.RegisteredLinkTypes[ArtifactLinkIds.PullRequest],
                                            $"vstfs:///Git/PullRequestId/{newGitRepo.ProjectReference.Id}%2f{newGitRepo.Id}%2f{oldCommitId}");
                                        break;

                                    default:
                                        Trace.WriteLine(String.Format("Skipping unsupported link type {0}", l.ArtifactLinkType.Name));
                                        break;
                                }

                               if(newLink != null)
                               {
                                    var elinks = from Link lq in workitem.Links
                                                 where gitWits.Contains(lq.ArtifactLinkType.Name)
                                                 select (ExternalLink)lq;
                                    var found =
                                    (from Link lq in elinks
                                     where (((ExternalLink)lq).LinkedArtifactUri.ToLower() == newLink.LinkedArtifactUri.ToLower())
                                     select lq).SingleOrDefault();
                                    if (found == null)
                                    {
                                        newEL.Add(newLink);
                                    }
                                    removeEL.Add(el);
                               }
                            }
                            else
                            {
                                Trace.WriteLine($"FAIL: cannot map {oldGitRepo.RemoteUrl} to ???");
                            }
                        }
                        else
                        {
                            Trace.WriteLine($"FAIL could not find source git repo");
                            noteFound++;
                        }
                    }
                }
                // add and remove
                foreach (ExternalLink eln in newEL)
                {
                    try
                    {
                        Trace.WriteLine("Adding " + eln.LinkedArtifactUri, Name);
                        workitem.Links.Add(eln);

                    }
                    catch (Exception)
                    {

                        // eat exception as sometimes TFS thinks this is an attachment
                    }
                }
                foreach (ExternalLink elr in removeEL)
                {
                    if (workitem.Links.Contains(elr))
                    {
                        try
                        {
                            Trace.WriteLine("Removing " + elr.LinkedArtifactUri, Name);
                            workitem.Links.Remove(elr);
                        }
                        catch (Exception)
                        {

                            // eat exception as sometimes TFS thinks this is an attachment
                        }
                    }
                }

                if (workitem.IsDirty)
                {
                    Trace.WriteLine($"Saving {workitem.Id}");
                    workitem.Save();
                }

                witstopwatch.Stop();
                elapsedms = elapsedms + witstopwatch.ElapsedMilliseconds;
                current--;
                count++;
                TimeSpan average = new TimeSpan(0, 0, 0, 0, (int) (elapsedms / count));
                TimeSpan remaining = new TimeSpan(0, 0, 0, 0, (int) (average.TotalMilliseconds * current));
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