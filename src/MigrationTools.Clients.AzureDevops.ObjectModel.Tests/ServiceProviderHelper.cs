﻿using Microsoft.Extensions.DependencyInjection;

namespace MigrationTools.Tests
{
    internal static class ServiceProviderHelper
    {
        internal static ServiceProvider GetServices()
        {
            var services = new ServiceCollection();
            services.AddMigrationToolServicesForUnitTests();

            services.AddMigrationToolServices();
            services.AddMigrationToolServicesForClientAzureDevOpsObjectModel();

            return services.BuildServiceProvider();
        }
    }
}