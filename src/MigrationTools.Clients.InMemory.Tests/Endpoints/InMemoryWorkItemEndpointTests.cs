﻿using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MigrationTools.DataContracts;
using MigrationTools.Tests;

namespace MigrationTools.Endpoints.Tests
{
    [TestClass()]
    public class InMemoryWorkItemEndpointTests
    {
        public ServiceProvider Services { get; private set; }

        [TestInitialize]
        public void Setup()
        {
            Services = ServiceProviderHelper.GetServices();
        }

        [TestMethod, TestCategory("L1")]
        public void ConfiguredTest()
        {
            InMemoryWorkItemEndpoint e = CreateAndConfigureInMemoryWorkItemEndpoint(10);
            Assert.AreEqual(10, e.Count);
        }

        [TestMethod(), TestCategory("L1")]
        public void EmptyTest()
        {
            var targetOptions = new InMemoryWorkItemEndpointOptions();
            InMemoryWorkItemEndpoint e = Services.GetRequiredService<InMemoryWorkItemEndpoint>();
            e.Configure(targetOptions);
            Assert.AreEqual(0, e.Count);
        }

        [TestMethod, TestCategory("L1")]
        public void FilterAllTest()
        {
            InMemoryWorkItemEndpoint e1 = CreateAndConfigureInMemoryWorkItemEndpoint(10);
            InMemoryWorkItemEndpoint e2 = CreateAndConfigureInMemoryWorkItemEndpoint(10);
            e1.Filter(e2.GetWorkItems());
            Assert.AreEqual(0, e1.Count);
        }

        [TestMethod, TestCategory("L1")]
        public void FilterHalfTest()
        {
            InMemoryWorkItemEndpoint e1 = CreateAndConfigureInMemoryWorkItemEndpoint(20);
            InMemoryWorkItemEndpoint e2 = CreateAndConfigureInMemoryWorkItemEndpoint(10);
            e1.Filter(e2.GetWorkItems());
            Assert.AreEqual(10, e1.Count);
        }

        [TestMethod, TestCategory("L1")]
        public void PersistWorkItemExistsTest()
        {
            InMemoryWorkItemEndpoint e1 = CreateAndConfigureInMemoryWorkItemEndpoint(20);
            InMemoryWorkItemEndpoint e2 = CreateAndConfigureInMemoryWorkItemEndpoint(10);
            foreach (WorkItemData item in e1.GetWorkItems())
            {
                e2.PersistWorkItem(item);
            }
            Assert.AreEqual(20, e2.Count);
        }

        [TestMethod, TestCategory("L1")]
        public void PersistWorkItemWithFilterTest()
        {
            InMemoryWorkItemEndpoint e1 = CreateAndConfigureInMemoryWorkItemEndpoint(20);
            InMemoryWorkItemEndpoint e2 = CreateAndConfigureInMemoryWorkItemEndpoint(10);
            e1.Filter(e2.GetWorkItems());
            Assert.AreEqual(10, e1.Count);
            foreach (WorkItemData item in e1.GetWorkItems())
            {
                e2.PersistWorkItem(item);
            }
            Assert.AreEqual(20, e2.Count);
        }

        private InMemoryWorkItemEndpoint CreateAndConfigureInMemoryWorkItemEndpoint(int workItemCount)
        {
            InMemoryWorkItemEndpoint e = CreateInMemoryWorkItemEndpoint();
            AddWorkItems(e, workItemCount);
            return e;
        }

        private InMemoryWorkItemEndpoint CreateInMemoryWorkItemEndpoint()
        {
            var options = new InMemoryWorkItemEndpointOptions();
            InMemoryWorkItemEndpoint e = Services.GetRequiredService<InMemoryWorkItemEndpoint>();
            e.Configure(options);
            return e;
        }

        private void AddWorkItems(InMemoryWorkItemEndpoint e, int workItemCount)
        {
            var list = new List<WorkItemData>();
            for (int i = 0; i < workItemCount; i++)
            {
                e.PersistWorkItem(new WorkItemData()
                {
                    Id = i.ToString(),
                    Revisions = GetRevisions()
                });
            }

            List<RevisionItem> GetRevisions()
            {
                Random rand = new Random();
                int revCount = rand.Next(0, 5);
                List<RevisionItem> list = new List<RevisionItem>();
                for (int i = 0; i < revCount; i++)
                {
                    list.Add(new RevisionItem { Index = i, Number = i, ChangedDate = DateTime.Now.AddHours(-i) });
                }
                return list;
            }
        }
    }
}