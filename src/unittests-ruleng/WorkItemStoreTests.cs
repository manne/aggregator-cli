﻿using System;
using System.Collections.Generic;
using System.Linq;
using aggregator;
using aggregator.Engine;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using NSubstitute;
using Xunit;

namespace unittests_ruleng
{
    public class WorkItemStoreTests
    {
        const string collectionUrl = "https://dev.azure.com/fake-organization";
        Guid projectId = Guid.NewGuid();
        const string projectName = "test-project";
        const string personalAccessToken = "***personalAccessToken***";
        string workItemsBaseUrl = $"{collectionUrl}/{projectName}/_apis/wit/workItems";

        [Fact]
        public void GetWorkItem_ById_Succeeds()
        {
            var logger = Substitute.For<IAggregatorLogger>();
            var client = Substitute.For<WorkItemTrackingHttpClientBase>(new Uri($"{collectionUrl}"), null);
            int workItemId = 42;
            client.GetWorkItemAsync(workItemId, expand: WorkItemExpand.All).Returns(new WorkItem()
            {
                Id = workItemId,
                Fields = new Dictionary<string, object>() {}
            });

            var context = new EngineContext(client, projectId, projectName, personalAccessToken, logger);
            var sut = new WorkItemStore(context);

            var wi = sut.GetWorkItem(workItemId);

            Assert.NotNull(wi);
            Assert.Equal(workItemId, wi.Id.Value);
        }

        [Fact]
        public void GetWorkItems_ByIds_Succeeds()
        {
            var logger = Substitute.For<IAggregatorLogger>();
            var client = Substitute.For<WorkItemTrackingHttpClientBase>(new Uri($"{collectionUrl}"), null);
            var ids = new int[] { 42, 99 };
            client.GetWorkItemsAsync((IEnumerable<int>)ids, expand: WorkItemExpand.All)
                .ReturnsForAnyArgs(new List<WorkItem>() {
                    new WorkItem()
                    {
                        Id = ids[0],
                        Fields = new Dictionary<string, object>() {}
                    },
                    new WorkItem()
                    {
                        Id = ids[1],
                        Fields = new Dictionary<string, object>() {}
                    }
                });

            var context = new EngineContext(client, projectId, projectName, personalAccessToken, logger);
            var sut = new WorkItemStore(context);

            var wis = sut.GetWorkItems(ids);

            Assert.NotEmpty(wis);
            Assert.Equal(2, wis.Count);
            Assert.Contains(wis, (x) => x.Id.Value == 42);
            Assert.Contains(wis, (x) => x.Id.Value == 99);
        }

        [Fact]
        public void NewWorkItem_Succeeds()
        {
            var logger = Substitute.For<IAggregatorLogger>();
            var client = Substitute.For<WorkItemTrackingHttpClientBase>(new Uri($"{collectionUrl}"), null);
            var context = new EngineContext(client, projectId, projectName, personalAccessToken, logger);
            var sut = new WorkItemStore(context);

            var wi = sut.NewWorkItem("Task");
            wi.Title = "Brand new";
            var save = sut.SaveChanges(SaveMode.Default, false).Result;

            Assert.NotNull(wi);
            Assert.True(wi.IsNew);
            Assert.Equal(1, save.created);
            Assert.Equal(0, save.updated);
            Assert.Equal(-1, wi.Id.Value);
        }

        [Fact]
        public void AddChild_Succeeds()
        {
            var logger = Substitute.For<IAggregatorLogger>();
            var client = Substitute.For<WorkItemTrackingHttpClientBase>(new Uri($"{collectionUrl}"), null);
            var context = new EngineContext(client, projectId, projectName, personalAccessToken, logger);
            int workItemId = 1;
            client.GetWorkItemAsync(workItemId, expand: WorkItemExpand.All).Returns(new WorkItem()
            {
                Id = workItemId,
                Fields = new Dictionary<string, object>() {},
                Relations = new List<WorkItemRelation>()
                {
                    new WorkItemRelation
                    {
                        Rel = "System.LinkTypes.Hierarchy-Forward",
                        Url = $"{workItemsBaseUrl}/42"
                    },
                    new WorkItemRelation
                    {
                        Rel = "System.LinkTypes.Hierarchy-Forward",
                        Url = $"{workItemsBaseUrl}/99"
                    }
                },
            });

            var sut = new WorkItemStore(context);

            var parent = sut.GetWorkItem(1);
            Assert.Equal(2, parent.Relations.Count());

            var newChild = sut.NewWorkItem("Task");
            newChild.Title = "Brand new";
            parent.Relations.AddChild(newChild);

            Assert.NotNull(newChild);
            Assert.True(newChild.IsNew);
            Assert.Equal(-1, newChild.Id.Value);
            Assert.Equal(3, parent.Relations.Count());
        }
    }
}
