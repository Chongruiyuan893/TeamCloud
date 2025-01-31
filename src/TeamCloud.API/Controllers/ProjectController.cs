/**
 *  Copyright (c) Microsoft Corporation.
 *  Licensed under the MIT License.
 */

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Schema;
using Swashbuckle.AspNetCore.Annotations;
using TeamCloud.API.Auth;
using TeamCloud.API.Data;
using TeamCloud.API.Data.Results;
using TeamCloud.API.Data.Validators;
using TeamCloud.API.Services;
using TeamCloud.Data;
using TeamCloud.Model.Commands;
using TeamCloud.Model.Data;
using ValidationError = TeamCloud.API.Data.Results.ValidationError;

namespace TeamCloud.API.Controllers
{
    [ApiController]
    [Route("orgs/{organizationId:organizationId}/projects")]
    [Produces("application/json")]
    public class ProjectController : ApiController
    {
        private readonly IProjectRepository projectRepository;
        private readonly IProjectTemplateRepository projectTemplateRepository;

        public ProjectController(IProjectRepository projectRepository, IProjectTemplateRepository projectTemplateRepository) : base()
        {
            this.projectRepository = projectRepository ?? throw new ArgumentNullException(nameof(projectRepository));
            this.projectTemplateRepository = projectTemplateRepository ?? throw new ArgumentNullException(nameof(projectTemplateRepository));
        }

        private async Task<List<User>> ResolveUsersAsync(string organizationId, ProjectDefinition projectDefinition, string projectId)
        {
            var users = new List<User>();

            if (projectDefinition.Users?.Any() ?? false)
            {
                var tasks = projectDefinition.Users.Select(user => ResolveUserAndEnsureMembershipAsync(user, projectId));
                users = (await Task.WhenAll(tasks).ConfigureAwait(false)).ToList();
            }

            if (!users.Any(u => u.Id == UserService.CurrentUserId))
            {
                var currentUser = await UserService
                    .CurrentUserAsync(organizationId)
                    .ConfigureAwait(false);

                currentUser.EnsureProjectMembership(projectId, ProjectUserRole.Owner);

                users.Add(currentUser);
            }

            return users;

            async Task<User> ResolveUserAndEnsureMembershipAsync(UserDefinition userDefinition, string projectId)
            {
                var user = await UserService
                    .ResolveUserAsync(organizationId, userDefinition)
                    .ConfigureAwait(false);

                var role = user.Id == UserService.CurrentUserId ? ProjectUserRole.Owner : Enum.Parse<ProjectUserRole>(userDefinition.Role, true);
                user.EnsureProjectMembership(projectId, role, userDefinition.Properties);

                return user;
            }
        }


        [HttpGet]
        [Authorize(Policy = AuthPolicies.OrganizationMember)]
        [SwaggerOperation(OperationId = "GetProjects", Summary = "Gets all Projects.")]
        [SwaggerResponse(StatusCodes.Status200OK, "Returns all Projects.", typeof(DataResult<List<Project>>))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "A validation error occured.", typeof(ErrorResult))]
        public Task<IActionResult> Get() => ExecuteAsync(async (user, organization) =>
        {
            var projects = await projectRepository
                .ListAsync(organization.Id)
                .ToListAsync()
                .ConfigureAwait(false);

            return DataResult<List<Project>>
                .Ok(projects)
                .ToActionResult();
        });


        [HttpGet("{projectId:projectId}")]
        [Authorize(Policy = AuthPolicies.ProjectMember)]
        [SwaggerOperation(OperationId = "GetProject", Summary = "Gets a Project.")]
        [SwaggerResponse(StatusCodes.Status200OK, "Returns a Project.", typeof(DataResult<Project>))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "A validation error occured.", typeof(ErrorResult))]
        [SwaggerResponse(StatusCodes.Status404NotFound, "A Project with the specified Name or ID was not found.", typeof(ErrorResult))]
        [SuppressMessage("Usage", "CA1801: Review unused parameters", Justification = "Used by base class and makes signiture unique")]
        public Task<IActionResult> Get([FromRoute] string projectId) => ExecuteAsync((user, organization, project) =>
        {
            return DataResult<Project>
                .Ok(project)
                .ToActionResultAsync();
        });


        [HttpPost]
        [Authorize(Policy = AuthPolicies.OrganizationMember)]
        [Consumes("application/json")]
        [SwaggerOperation(OperationId = "CreateProject", Summary = "Creates a new Project.")]
        [SwaggerResponse(StatusCodes.Status201Created, "The new Project was created.", typeof(DataResult<Project>))]
        [SwaggerResponse(StatusCodes.Status202Accepted, "Started creating the new Project. Returns a StatusResult object that can be used to track progress of the long-running operation.", typeof(StatusResult))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "A validation error occured.", typeof(ErrorResult))]
        [SwaggerResponse(StatusCodes.Status409Conflict, "A Project already exists with the name specified in the request body.", typeof(ErrorResult))]
        public Task<IActionResult> Post([FromBody] ProjectDefinition projectDefinition) => ExecuteAsync(async (user, organization) =>
        {
            if (projectDefinition is null)
                throw new ArgumentNullException(nameof(projectDefinition));

            var validation = new ProjectDefinitionValidator().Validate(projectDefinition);

            if (!validation.IsValid)
                return ErrorResult
                    .BadRequest(validation)
                    .ToActionResult();

            var nameExists = await projectRepository
                .NameExistsAsync(organization.Id, projectDefinition.DisplayName)
                .ConfigureAwait(false);

            if (nameExists)
                return ErrorResult
                    .Conflict($"A Project with name '{projectDefinition.DisplayName}' already exists. Project names must be unique. Please try your request again with a unique name.")
                    .ToActionResult();

            var projectId = Guid.NewGuid().ToString();

            var users = await ResolveUsersAsync(organization.Id, projectDefinition, projectId)
                .ConfigureAwait(false);

            var project = new Project
            {
                Id = projectId,
                Organization = organization.Id,
                Users = users,
                DisplayName = projectDefinition.DisplayName
            };

            ProjectTemplate template = null;

            if (!string.IsNullOrEmpty(projectDefinition.Template))
            {
                template = await projectTemplateRepository
                    .GetAsync(organization.Id, projectDefinition.Template)
                    .ConfigureAwait(false);

                if (template is null)
                    return ErrorResult
                        .BadRequest(new ValidationError { Field = "template", Message = $"A Project Template with the ID '{projectDefinition.Template}' could not be found in this Organization. Please try your request again with a valid Project Template ID for 'template'." })
                        .ToActionResult();
            }
            else
            {
                template = await projectTemplateRepository
                    .GetDefaultAsync(organization.Id)
                    .ConfigureAwait(false);

                if (template is null)
                    return ErrorResult
                        .BadRequest(new ValidationError { Field = "template", Message = $"No value was provided for 'template' and there is no a default Project Template set for this Organization. Please try your request again with a valid Project Template ID for 'template'." })
                        .ToActionResult();
            }

            var input = JObject.Parse(projectDefinition.TemplateInput);
            var schema = JSchema.Parse(template.InputJsonSchema);

            if (!input.IsValid(schema, out IList<string> schemaErrors))
                return ErrorResult
                    .BadRequest(new ValidationError { Field = "templateInput", Message = $"Project templateInput does not match the the Project Template's inputJsonSchema.  Errors: {string.Join(", ", schemaErrors)}." })
                    .ToActionResult();

            project.Template = template.Id;
            project.TemplateInput = projectDefinition.TemplateInput;

            project.Tags = input
                .ToObject<Dictionary<string, object>>()
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value?.ToString());

            var currentUser = users.FirstOrDefault(u => u.Id == UserService.CurrentUserId);

            var command = new ProjectCreateCommand(currentUser, project);

            return await Orchestrator
                .InvokeAndReturnActionResultAsync(command, Request)
                .ConfigureAwait(false);
        });


        [HttpDelete("{projectId:projectId}")]
        [Authorize(Policy = AuthPolicies.ProjectOwner)]
        [SwaggerOperation(OperationId = "DeleteProject", Summary = "Deletes a Project.")]
        [SwaggerResponse(StatusCodes.Status202Accepted, "Starts deleting the specified Project. Returns a StatusResult object that can be used to track progress of the long-running operation.", typeof(StatusResult))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "A validation error occured.", typeof(ErrorResult))]
        [SwaggerResponse(StatusCodes.Status404NotFound, "A Project with the specified name or ID was not found.", typeof(ErrorResult))]
        [SuppressMessage("Usage", "CA1801: Review unused parameters", Justification = "Used by base class and makes signiture unique")]
        public Task<IActionResult> Delete([FromRoute] string projectId) => ExecuteAsync(async (user, organization, project) =>
        {
            var command = new ProjectDeleteCommand(user, project);

            return await Orchestrator
                .InvokeAndReturnActionResultAsync(command, Request)
                .ConfigureAwait(false);
        });
    }
}
