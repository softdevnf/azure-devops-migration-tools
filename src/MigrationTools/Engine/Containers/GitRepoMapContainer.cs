﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using MigrationTools.Configuration;

namespace MigrationTools.Engine.Containers
{
    public class GitRepoMapContainer : EngineContainer<ReadOnlyDictionary<string, string>>
    {
        private Dictionary<string, string> GitRepoMaps { get; set; }

        public override ReadOnlyDictionary<string, string> Items { get { return new ReadOnlyDictionary<string, string>(GitRepoMaps); } }

        public GitRepoMapContainer(IServiceProvider services, EngineConfiguration config) : base(services, config)
        {
            GitRepoMaps = new Dictionary<string, string>();
        }

        protected override void Configure()
        {
            if (Config.GitRepoMapping != null)
            {
                GitRepoMaps = Config.GitRepoMapping ?? new Dictionary<string, string>();
            }
        }
    }
}