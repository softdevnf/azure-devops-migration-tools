﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.TeamFoundation;
using Microsoft.TeamFoundation.Client;
using Microsoft.VisualStudio.Services.Common;
using MigrationTools.Core;
using MigrationTools.Core.Clients;
using MigrationTools.Core.Configuration;
using MigrationTools.Core.DataContracts;
using Serilog;

namespace MigrationTools.Clients.AzureDevops.ObjectModel.Clients
{
    public class MigrationClient : IMigrationClient
    {
        private TeamProjectConfig _config;
        private NetworkCredential _credentials;
        private IWorkItemMigrationClient _workItemClient;

        private readonly IServiceProvider _Services;
        private readonly ITelemetryLogger _Telemetry;

        public TeamProjectConfig Config
        {
            get
            {
                return _config;
            }
        }

        public IWorkItemMigrationClient WorkItems
        {
            get
            {
                return _workItemClient;
            }
        }

        // if you add Migration Engine in here you will have to fix the infinate loop
        public MigrationClient(IWorkItemMigrationClient workItemClient, IServiceProvider services, ITelemetryLogger telemetry)
        {
            _workItemClient = workItemClient;
            _Services = services;
            _Telemetry = telemetry;
        }


        public void Configure(TeamProjectConfig config, NetworkCredential credentials = null)
        {
            _config = config;
            _credentials = credentials;
            EnsureCollection();
            _workItemClient.Configure(this);
        }

        private TfsTeamProjectCollection _collection;
        [Obsolete]
        public object InternalCollection
        {
            get
            {
                return _collection;
            }
        }

        private void EnsureCollection()
        {
            if (_collection == null)
            {
                _Telemetry.TrackEvent("TeamProjectContext.Connect",
                    new Dictionary<string, string> {
                          { "Name", Config.Project},
                          { "Target Project", Config.Project},
                          { "Target Collection",Config.Collection.ToString() },
                           { "ReflectedWorkItemID Field Name",Config.ReflectedWorkItemIDFieldName }
                    }, null);
                Stopwatch connectionTimer = Stopwatch.StartNew();
                DateTime start = DateTime.Now;
                Log.Information("Connecting to {@Config}", Config);

                if (_credentials == null)
                    _collection = new TfsTeamProjectCollection(Config.Collection);
                else
                    _collection = new TfsTeamProjectCollection(Config.Collection, new VssCredentials(new Microsoft.VisualStudio.Services.Common.WindowsCredential(_credentials)));

                try
                {
                    Log.Debug("Connected to {CollectionUrl} ", _collection.Uri.ToString());
                    Log.Debug("validating security for {@AuthorizedIdentity} ", _collection.AuthorizedIdentity);
                    _collection.EnsureAuthenticated();
                    connectionTimer.Stop();
                    _Telemetry.TrackDependency(new DependencyTelemetry("TeamService", "EnsureAuthenticated", start, connectionTimer.Elapsed, true));
                    Log.Information(" Access granted ");
                }
                catch (TeamFoundationServiceUnavailableException ex)
                {
                    _Telemetry.TrackDependency(new DependencyTelemetry("TeamService", "EnsureAuthenticated", start, connectionTimer.Elapsed, false));
                    _Telemetry.TrackException(ex,
                       new Dictionary<string, string> {
                            { "CollectionUrl", Config.Collection.ToString() },
                            { "TeamProjectName",  Config.Project}
                       },
                       new Dictionary<string, double> {
                            { "ConnectionTimer", connectionTimer.ElapsedMilliseconds }
                       });
                    Log.Error(ex, "Unable to connect to {@Config}", Config);
                    throw;
                }
            }
        }

        public T GetService<T>()
        {
            EnsureCollection();
            return _collection.GetService<T>();
        }

    }
}
