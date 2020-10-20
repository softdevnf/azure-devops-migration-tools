﻿using System.Collections.ObjectModel;
using MigrationTools.Enrichers;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace MigrationTools.Endpoints
{
    public enum EndpointDirection
    {
        Source,
        Target
    }

    public interface IEndpointOptions
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public EndpointDirection Direction { get; set; }

        public Collection<IWorkItemEnricher> WorkItemEnrichers { get; set; }
    }

    public class EndpointOptions : IEndpointOptions
    {
        [JsonConverter(typeof(StringEnumConverter))]
        public EndpointDirection Direction { get; set; }

        public Collection<IWorkItemEnricher> WorkItemEnrichers { get; set; }
    }
}