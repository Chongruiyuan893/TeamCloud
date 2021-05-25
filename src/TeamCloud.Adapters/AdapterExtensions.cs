﻿/**
 *  Copyright (c) Microsoft Corporation.
 *  Licensed under the MIT License.
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Azure.Cosmos.Table;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Newtonsoft.Json;
using Newtonsoft.Json.Schema;
using TeamCloud.Adapters.Authorization;

namespace TeamCloud.Adapters
{
    public static class AdapterExtensions
    {
        public static string ToString(this JSchema schema, Formatting formatting)
        {
            if (schema is null)
                throw new ArgumentNullException(nameof(schema));

            var sb = new StringBuilder();

            using var sw = new StringWriter(sb);
            using var jw = new JsonTextWriter(sw) { Formatting = formatting };

            schema.WriteTo(jw);

            return sb.ToString();
        }

        public static IServiceCollection AddTeamCloudAdapterFramework(this IServiceCollection services)
        {
            if (services is null)
                throw new ArgumentNullException(nameof(services));

            services
                .TryAddTransient(provider => AuthorizationSessionOptions.Default);

            services
                .TryAddSingleton<IAuthorizationSessionClient, AuthorizationSessionClient>();

            services
                .TryAddTransient(provider => AuthorizationTokenOptions.Default);

            services
                .TryAddSingleton<IAuthorizationTokenClient, AuthorizationTokenClient>();

            return services;
        }

        public static IServiceCollection AddTeamCloudAdapter<TAdapter>(this IServiceCollection services)
            where TAdapter : class, IAdapter
        {
            if (services is null)
                throw new ArgumentNullException(nameof(services));

            services
                .AddTeamCloudAdapterFramework()
                .AddSingleton<IAdapter, TAdapter>();

            return services;
        }
    }
}
