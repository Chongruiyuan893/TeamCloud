﻿/**
 *  Copyright (c) Microsoft Corporation.
 *  Licensed under the MIT License.
 */

using System;

namespace TeamCloud.Azure.Directory
{
    public sealed class AzureServicePrincipal
    {
        public Guid ObjectId { get; internal set; }
        public Guid ApplicationId { get; internal set; }
        public Guid TenantId { get; internal set; }
        public string Name { get; internal set; }
        public string Password { get; internal set; }
        public DateTime? ExpiresOn { get; internal set; }
    }
}
