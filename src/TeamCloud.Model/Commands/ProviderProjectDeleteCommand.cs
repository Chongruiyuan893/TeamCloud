﻿/**
 *  Copyright (c) Microsoft Corporation.
 *  Licensed under the MIT License.
 */

using System;
using TeamCloud.Model.Data;

namespace TeamCloud.Model.Commands
{
    public class ProviderProjectDeleteCommand : ProviderCommand<Project, ProviderProjectDeleteCommandResult>
    {
        public ProviderProjectDeleteCommand(Uri baseApi, User user, Project payload, Guid? commandId = null) : base(baseApi, user, payload, commandId)
            => ProjectId = !string.IsNullOrEmpty(payload?.Id) ? payload.Id : throw new ArgumentNullException(nameof(payload));
    }
}
