﻿/**
 *  Copyright (c) Microsoft Corporation.
 *  Licensed under the MIT License.
 */

using System;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using TeamCloud.Data;
using TeamCloud.Model.Data;
using TeamCloud.Orchestration;
using TeamCloud.Orchestrator.Entities;

namespace TeamCloud.Orchestrator.Activities
{
    public class ProjectGetActivity
    {
        private readonly IProjectRepository projectsRepository;

        public ProjectGetActivity(IProjectRepository projectsRepository)
        {
            this.projectsRepository = projectsRepository ?? throw new ArgumentNullException(nameof(projectsRepository));
        }

        [FunctionName(nameof(ProjectGetActivity))]
        public async Task<ProjectDocument> RunActivity(
            [ActivityTrigger] string projectId)
        {
            return await projectsRepository
                .GetAsync(projectId)
                .ConfigureAwait(false);
        }
    }

    internal static class ProjectGetExtension
    {
        public static Task<ProjectDocument> GetProjectAsync(this IDurableOrchestrationContext functionContext, string projectId, bool allowUnsafe = false)
            => functionContext.IsLockedBy<ProjectDocument>(projectId) || allowUnsafe
            ? functionContext.CallActivityWithRetryAsync<ProjectDocument>(nameof(ProjectGetActivity), projectId)
            : throw new NotSupportedException($"Unable to get project '{projectId}' without acquired lock");
    }
}
