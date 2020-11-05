﻿using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using Microsoft.Extensions.Logging;
using Microsoft.TeamFoundation.Client;
using Microsoft.TeamFoundation.Framework.Client;
using Microsoft.TeamFoundation.Framework.Common;
using MigrationTools;
using MigrationTools._EngineV1.CommandLine;
using MigrationTools.CommandLine;

namespace VstsSyncMigrator.Commands
{
    public class ExportADGroupsCommand : CommandBase
    {
        public static object Run(ExportADGroupsOptions opts, string logPath, ITelemetryLogger telemetry, ILogger<ExportADGroupsCommand> logger)
        {
            telemetry.TrackEvent("Run-ExportADGroupsCommand");
            string exportPath = CreateExportPath(logPath, "ExportADGroups");
            Stopwatch stopwatch = Stopwatch.StartNew();
            //////////////////////////////////////////////////

            StreamWriter sw = File.CreateText(Path.Combine(exportPath, "AzureADGroups.csv"));
            sw.AutoFlush = true;
            using (var csv = new CsvWriter(sw, CultureInfo.InvariantCulture))
            {
                csv.WriteHeader<AzureAdGroupItem>();

                TfsTeamProjectCollection sourceCollection = new TfsTeamProjectCollection(opts.CollectionURL);
                sourceCollection.EnsureAuthenticated();
                IIdentityManagementService2 sourceIMS2 = (IIdentityManagementService2)sourceCollection.GetService(typeof(IIdentityManagementService2));
                List<CatalogNode> sourceTeamProjects = sourceCollection.CatalogNode.QueryChildren(new[] { CatalogResourceTypes.TeamProject }, false, CatalogQueryOptions.None).ToList();
                if (opts.TeamProject != null)
                {
                    sourceTeamProjects = sourceTeamProjects.Where(x => x.Resource.DisplayName == opts.TeamProject).ToList();
                }
                int current = sourceTeamProjects.Count();
                foreach (CatalogNode sourceTeamProject in sourceTeamProjects)
                {
                    logger.LogInformation("---------------{0}\\{1}", current, sourceTeamProjects.Count());
                    logger.LogInformation("{0}, {1}", sourceTeamProject.Resource.DisplayName, sourceTeamProject.Resource.Identifier);
                    string projectUri = sourceTeamProject.Resource.Properties["ProjectUri"];
                    TeamFoundationIdentity[] appGroups = sourceIMS2.ListApplicationGroups(projectUri, ReadIdentityOptions.None);
                    foreach (TeamFoundationIdentity appGroup in appGroups.Where(x => !x.DisplayName.EndsWith("\\Project Valid Users")))
                    {
                        logger.LogInformation("    {0}", appGroup.DisplayName);
                        TeamFoundationIdentity sourceAppGroup = sourceIMS2.ReadIdentity(appGroup.Descriptor, MembershipQuery.Expanded, ReadIdentityOptions.None);
                        foreach (IdentityDescriptor child in sourceAppGroup.Members.Where(x => x.IdentityType == "Microsoft.TeamFoundation.Identity"))
                        {
                            TeamFoundationIdentity sourceChildIdentity = sourceIMS2.ReadIdentity(IdentitySearchFactor.Identifier, child.Identifier, MembershipQuery.None, ReadIdentityOptions.ExtendedProperties);

                            if ((string)sourceChildIdentity.GetProperty("SpecialType") == "AzureActiveDirectoryApplicationGroup")
                            {
                                logger.LogInformation("     Suspected AD Group {0}", sourceChildIdentity.DisplayName);
                                csv.WriteRecord<AzureAdGroupItem>(new AzureAdGroupItem
                                {
                                    TeamProject = sourceTeamProject.Resource.DisplayName,
                                    ApplciationGroup = sourceTeamProject.Resource.DisplayName,
                                    Account = (string)sourceChildIdentity.GetProperty("Account"),
                                    Mail = (string)sourceChildIdentity.GetProperty("Mail"),
                                    DirectoryAlias = (string)sourceChildIdentity.GetProperty("DirectoryAlias")
                                });
                            }
                        }
                    }
                    current--;
                    sw.Flush();
                }
            }
            sw.Close();
            //    current--;
            //}

            //////////////////////////////////////////////////
            stopwatch.Stop();
            logger.LogInformation("DONE in {Elapsed}", stopwatch.Elapsed.ToString("c"));
            return 0;
        }
    }
}