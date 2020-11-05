﻿using Microsoft.Extensions.DependencyInjection;
using MigrationTools.Endpoints;

namespace MigrationTools
{
    public static partial class ServiceCollectionExtensions
    {
        public static void AddMigrationToolServicesForClientInMemory(this IServiceCollection context)
        {
            context.AddTransient<InMemoryWorkItemEndpoint>();
        }
    }
}