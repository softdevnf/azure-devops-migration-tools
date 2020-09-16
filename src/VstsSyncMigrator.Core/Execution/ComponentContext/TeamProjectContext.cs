﻿using Microsoft.TeamFoundation.Client;
using System;
using System.Diagnostics;
using Microsoft.TeamFoundation;
using Microsoft.VisualStudio.Services.Common;
using System.Collections.Generic;
using System.Net;
using MigrationTools.Core.Configuration;
using Microsoft.TeamFoundation.WorkItemTracking.Client;
using MigrationTools;
using Serilog;

namespace VstsSyncMigrator.Engine
{
    public class TeamProjectContext : ITeamProjectContext
    {
        private TeamProjectConfig _config;
        private TfsTeamProjectCollection _Collection;
        private NetworkCredential _credentials;

        public TfsTeamProjectCollection Collection
        {
            get
            {
                Connect();
                return _Collection;
            }
        }

        public TeamProjectConfig Config
        {
            get
            {
                return _config;
            }
        }

        public TeamProjectContext(TeamProjectConfig config)
        {

            this._config = config;
        }

        public TeamProjectContext(TeamProjectConfig config, NetworkCredential credentials)
        {
            _config = config;
            _credentials = credentials;
        }

        public void Connect()
        {
            if (_Collection == null)
            {
                Telemetry.Current.TrackEvent("TeamProjectContext.Connect",
                    new Dictionary<string, string> {
                          { "Name", Config.Project},
                          { "Target Project", Config.Project},
                          { "Target Collection",Config.Collection.ToString() },
                           { "ReflectedWorkItemID Field Name",Config.ReflectedWorkItemIDFieldName }
                    });
                Stopwatch connectionTimer = Stopwatch.StartNew();
				DateTime start = DateTime.Now;
                Log.Information("Connecting to {@Config}", Config);

                if (_credentials == null)
                    _Collection = new TfsTeamProjectCollection(Config.Collection);
                else
                    _Collection = new TfsTeamProjectCollection(Config.Collection, new VssCredentials(new Microsoft.VisualStudio.Services.Common.WindowsCredential(_credentials)));
                
                try
                {
                    Log.Debug("Connected to {CollectionUrl} ", _Collection.Uri.ToString());
                    Log.Debug("validating security for {@AuthorizedIdentity} ", _Collection.AuthorizedIdentity);
                    _Collection.EnsureAuthenticated();
                    connectionTimer.Stop();
                    Telemetry.Current.TrackDependency("TeamService", "EnsureAuthenticated", start, connectionTimer.Elapsed, true);
                    Log.Information(" Access granted ");
                }
                catch (TeamFoundationServiceUnavailableException ex)
                {
                    Telemetry.Current.TrackDependency("TeamService", "EnsureAuthenticated", start, connectionTimer.Elapsed, false);
                    Telemetry.Current.TrackException(ex,
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
    }
}