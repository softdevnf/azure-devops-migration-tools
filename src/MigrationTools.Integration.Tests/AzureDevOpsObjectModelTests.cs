﻿using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MigrationTools.Endpoints;
using MigrationTools.Processors;
using MigrationTools.Tests;
using Serilog;
using Serilog.Events;

namespace MigrationTools.Integration.Tests
{
    [TestClass()]
    public class AzureDevOpsObjectModelTests
    {
        private ServiceProvider Services = ServiceProviderHelper.GetServicesV2();

        [TestInitialize]
        public void Setup()
        {
            var loggers = new LoggerConfiguration().MinimumLevel.Verbose().Enrich.FromLogContext();
            loggers.WriteTo.Logger(logger => logger
              .WriteTo.Debug(restrictedToMinimumLevel: LogEventLevel.Verbose));
            Log.Logger = loggers.CreateLogger();
            Log.Logger.Information("Logger is initialized");
        }

        [TestMethod(), TestCategory("L3")]
        public void TestTfsToTfsNoEnrichers()
        {
            // Senario 1 Migration from Tfs to Tfs with no Enrichers.
            var migrationConfig = GetConfigurationTfsToTfsNoEnrichers();
            var workItemMigrationProcessor = Services.GetRequiredService<WorkItemTrackingProcessor>();
            workItemMigrationProcessor.Configure(migrationConfig);
            workItemMigrationProcessor.Execute();
            Assert.AreEqual(ProcessingStatus.Complete, workItemMigrationProcessor.Status);
        }

        private static WorkItemTrackingProcessorOptions GetConfigurationTfsToTfsNoEnrichers()
        {
            // Tfs To Tfs
            var migrationConfig = new WorkItemTrackingProcessorOptions()
            {
                Enabled = true,
                CollapseRevisions = false,
                ReplayRevisions = true,
                WorkItemCreateRetryLimit = 5,
                PrefixProjectToNodes = false,
                Source = GetTfsWorkItemEndPointOptions("migrationSource1"),
                Target = GetTfsWorkItemEndPointOptions("migrationTarget1")
            };
            return migrationConfig;
        }

        private static TfsWorkItemEndpointOptions GetTfsWorkItemEndPointOptions(string project)
        {
            return new TfsWorkItemEndpointOptions()
            {
                Organisation = "https://dev.azure.com/nkdagility-preview/",
                Project = project,
                AuthenticationMode = AuthenticationMode.AccessToken,
                AccessToken = TestingConstants.AccessToken,
                Query = new Options.QueryOptions()
                {
                    Query = "SELECT [System.Id], [System.Tags] " +
                            "FROM WorkItems " +
                            "WHERE [System.TeamProject] = @TeamProject " +
                                "AND [System.WorkItemType] NOT IN ('Test Suite', 'Test Plan') " +
                            "ORDER BY [System.ChangedDate] desc",
                    Paramiters = new Dictionary<string, string>() { { "TeamProject", "migrationSource1" } }
                }
            };
        }
    }
}