﻿using Microsoft.TeamFoundation;
using Microsoft.TeamFoundation.Git.Client;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using MigrationTools.Clients.AzureDevops.ObjectModel;
using MigrationTools.Clients.AzureDevops.ObjectModel.Clients;
using MigrationTools;
using MigrationTools.Clients;
using MigrationTools.DataContracts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VstsSyncMigrator.Engine;
using MigrationTools.Enrichers;
using Microsoft.Extensions.Logging;

namespace MigrationTools.Clients.AzureDevops.ObjectModel.Enrichers
{
    public class GitRepositoryEnricher : WorkItemEnricher
    {
        IMigrationEngine _Engine;
        private readonly ILogger<GitRepositoryEnricher> _Logger;
        bool _save = true;
        bool _filter = true;
        GitRepositoryService sourceRepoService;
        IList<GitRepository> sourceRepos;
        IList<GitRepository> allSourceRepos;
        GitRepositoryService targetRepoService;
        IList<GitRepository> targetRepos;
        IList<GitRepository> allTargetRepos;
        List<string> gitWits;

        public GitRepositoryEnricher(IMigrationEngine engine, ILogger<GitRepositoryEnricher> logger) :base(engine, logger)
        {
            _Engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _Logger = logger ?? throw new ArgumentNullException(nameof(logger));

            sourceRepoService = engine.Source.GetService<GitRepositoryService>();
            sourceRepos = sourceRepoService.QueryRepositories(engine.Source.Config.Project);
            allSourceRepos = sourceRepoService.QueryRepositories("");
            //////////////////////////////////////////////////
            targetRepoService = engine.Target.GetService<GitRepositoryService>();
            targetRepos = targetRepoService.QueryRepositories(engine.Target.Config.Project);
            allTargetRepos = targetRepoService.QueryRepositories("");
            gitWits = new List<string>
                {
                    "Branch",
                    "Fixed in Commit",
                    "Pull Request",
                    "Fixed in Changeset"    //TFVC
                };
        }


        public override void Configure(bool save = true, bool filter = true)
        {
            _filter = filter;
            _save = save;
        }

        public override int Enrich(WorkItemData sourceWorkItem, WorkItemData targetWorkItem)
        {
            if (sourceWorkItem is null)
            {
                throw new ArgumentNullException(nameof(sourceWorkItem));
            }

            if (targetWorkItem is null)
            {
                throw new ArgumentNullException(nameof(targetWorkItem));
            }

            Log.LogInformation("GitRepositoryEnricher: Enriching {Id} To fix Git Repo Links", targetWorkItem.Id);
            List<ExternalLink> newEL = new List<ExternalLink>();
            List<ExternalLink> removeEL = new List<ExternalLink>();
            int count = 0;
            foreach (Link l in targetWorkItem.ToWorkItem().Links)
            {
                if (l is ExternalLink && gitWits.Contains(l.ArtifactLinkType.Name))
                {
                    ExternalLink el = (ExternalLink)l;

                    GitRepositoryInfo sourceRepoInfo = GitRepositoryInfo.Create(el, sourceRepos, Engine, sourceWorkItem?.ProjectName);
                    
                    // if sourceRepo is null ignore this link and keep processing further links
                    if (sourceRepoInfo == null) 
                    {
                        continue;
                    }

                    // if repo was not found in source project, try to find it by repoId in the whole project collection
                    if (sourceRepoInfo.GitRepo == null)
                    {
                        var anyProjectSourceRepoInfo = GitRepositoryInfo.Create(el, allSourceRepos, Engine, sourceWorkItem?.ProjectName);
                        // if repo is found in a different project and the repo Name is listed in repo mappings, use it
                        if (anyProjectSourceRepoInfo.GitRepo != null && Engine.GitRepoMaps.Items.ContainsKey(anyProjectSourceRepoInfo.GitRepo.Name))
                        {
                            sourceRepoInfo = anyProjectSourceRepoInfo;
                        }
                        else
                        {
                            Log.LogWarning("GitRepositoryEnricher: Could not find source git repo - repo referenced: {SourceRepoName}/{TargetRepoName}", anyProjectSourceRepoInfo?.GitRepo?.ProjectReference?.Name, anyProjectSourceRepoInfo?.GitRepo?.Name);
                        }
                    }

                    if (sourceRepoInfo.GitRepo != null)
                    {
                        string targetRepoName = GetTargetRepoName(Engine.GitRepoMaps.Items, sourceRepoInfo);
                        string sourceProjectName = sourceRepoInfo?.GitRepo?.ProjectReference?.Name ?? Engine.Target.Config.Project;
                        string targetProjectName = Engine.Target.Config.Project;

                        GitRepositoryInfo targetRepoInfo = GitRepositoryInfo.Create(targetRepoName, sourceRepoInfo, targetRepos);
                        // if repo was not found in the target project, try to find it in the whole target project collection
                        if (targetRepoInfo.GitRepo == null)
                        {
                            if (Engine.GitRepoMaps.Items.Values.Contains(targetRepoName))
                            {
                                var anyTargetRepoInCollectionInfo = GitRepositoryInfo.Create(targetRepoName, sourceRepoInfo, allTargetRepos);
                                if (anyTargetRepoInCollectionInfo.GitRepo != null)
                                {
                                    targetRepoInfo = anyTargetRepoInCollectionInfo;
                                }
                            }
                        }

                        // Fix commit links if target repo has been found
                        if (targetRepoInfo.GitRepo != null)
                        {
                            Log.LogDebug("GitRepositoryEnricher: Fixing {SourceRepoUrl} to {TargetRepoUrl}?", sourceRepoInfo.GitRepo.RemoteUrl, targetRepoInfo.GitRepo.RemoteUrl);

                            // Create External Link object
                            ExternalLink newLink = null;
                            switch (l.ArtifactLinkType.Name)
                            {
                                case "Branch":
                                    newLink = new ExternalLink(((WorkItemMigrationClient)Engine.Target.WorkItems).Store.RegisteredLinkTypes[ArtifactLinkIds.Branch],
                                        $"vstfs:///git/ref/{targetRepoInfo.GitRepo.ProjectReference.Id}%2f{targetRepoInfo.GitRepo.Id}%2f{sourceRepoInfo.CommitID}");
                                    break;

                                case "Fixed in Changeset":  // TFVC
                                case "Fixed in Commit":
                                    newLink = new ExternalLink(((WorkItemMigrationClient)Engine.Target.WorkItems).Store.RegisteredLinkTypes[ArtifactLinkIds.Commit],
                                        $"vstfs:///git/commit/{targetRepoInfo.GitRepo.ProjectReference.Id}%2f{targetRepoInfo.GitRepo.Id}%2f{sourceRepoInfo.CommitID}");
                                    break;
                                case "Pull Request":
                                    //newLink = new ExternalLink(targetStore.Store.RegisteredLinkTypes[ArtifactLinkIds.PullRequest],
                                    //    $"vstfs:///Git/PullRequestId/{targetRepoInfo.GitRepo.ProjectReference.Id}%2f{targetRepoInfo.GitRepo.Id}%2f{sourceRepoInfo.CommitID}");
                                    removeEL.Add(el);
                                    break;

                                default:
                                    Log.LogWarning("Skipping unsupported link type {0}", l.ArtifactLinkType.Name);
                                    break;
                            }

                            if (newLink != null)
                            {
                                var elinks = from Link lq in targetWorkItem.ToWorkItem().Links
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
                            Log.LogWarning("GitRepositoryEnricher: Target Repo not found. Cannot map {SourceRepoUrl} to ???", sourceRepoInfo.GitRepo.RemoteUrl);
                        }
                    }
                }
            }
            // add and remove
            foreach (ExternalLink eln in newEL)
            {
                try
                {
                    Log.LogDebug("GitRepositoryEnricher: Adding {LinkedArtifactUri} ", eln.LinkedArtifactUri);
                    targetWorkItem.ToWorkItem().Links.Add(eln);

                }
                catch (Exception)
                {

                    // eat exception as sometimes TFS thinks this is an attachment
                }
            }
            foreach (ExternalLink elr in removeEL)
            {
                if (targetWorkItem.ToWorkItem().Links.Contains(elr))
                {
                    try
                    {
                        Log.LogDebug("GitRepositoryEnricher: Removing {LinkedArtifactUri} ", elr.LinkedArtifactUri);
                        targetWorkItem.ToWorkItem().Links.Remove(elr);
                        count++;
                    }
                    catch (Exception)
                    {

                        // eat exception as sometimes TFS thinks this is an attachment
                    }
                }

            }

            if (targetWorkItem.ToWorkItem().IsDirty && _save)
            {
                Log.LogDebug("GitRepositoryEnricher: Saving {TargetWorkItemId}", targetWorkItem.Id);
                targetWorkItem.ToWorkItem().Fields["System.ChangedBy"].Value = "Migration";
                targetWorkItem.SaveToAzureDevOps();
            }
            return count;
        }

        private string GetTargetRepoName(ReadOnlyDictionary<string, string> gitRepoMappings, GitRepositoryInfo repoInfo)
        {
            if (gitRepoMappings.ContainsKey(repoInfo.GitRepo.Name))
            {
                return gitRepoMappings[repoInfo.GitRepo.Name];
            }
            else
            {
                return repoInfo.GitRepo.Name;
            }
        }
    }
}
