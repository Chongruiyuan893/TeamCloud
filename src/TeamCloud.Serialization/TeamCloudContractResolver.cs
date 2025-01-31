﻿/**
 *  Copyright (c) Microsoft Corporation.
 *  Licensed under the MIT License.
 */

using System;
using System.Collections.Specialized;
using System.Reflection;
using Microsoft.AspNetCore.DataProtection;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using TeamCloud.Serialization.Compress;
using TeamCloud.Serialization.Converter;
using TeamCloud.Serialization.Encryption;

namespace TeamCloud.Serialization
{
    public class TeamCloudContractResolver : CamelCasePropertyNamesContractResolver
    {
        private readonly IDataProtectionProvider dataProtectionProvider;

        public TeamCloudContractResolver(IDataProtectionProvider dataProtectionProvider = null)
        {
            // prevent changing the case of dictionary keys
            NamingStrategy = new TeamCloudNamingStrategy();

            this.dataProtectionProvider = dataProtectionProvider;
        }

        protected override JsonContract CreateContract(Type objectType)
        {
            var contract = base.CreateContract(objectType);

            return contract;
        }

        protected override JsonConverter ResolveContractConverter(Type objectType)
        {
            if (objectType is null)
                throw new ArgumentNullException(nameof(objectType));

            if (typeof(Exception).IsAssignableFrom(objectType))
                return new ExceptionConverter();

            if (typeof(NameValueCollection).IsAssignableFrom(objectType))
                return new NameValueCollectionConverter();

            if (objectType.IsEnum)
                return new StringEnumConverter();

            return base.ResolveContractConverter(objectType);
        }

        protected override IValueProvider CreateMemberValueProvider(MemberInfo member)
        {
            if (member is null)
                throw new ArgumentNullException(nameof(member));

            var valueProvider = base.CreateMemberValueProvider(member);

            if (member.GetCustomAttribute<EncryptedAttribute>() != null)
                return new EncryptedValueProvider(member, valueProvider, dataProtectionProvider);
            else if (member.GetCustomAttribute<CompressAttribute>() != null)
                return new CompressValueProvider(member, valueProvider);

            return valueProvider; // we stick with the default value provider
        }

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var prop = base.CreateProperty(member, memberSerialization);

            if (member is PropertyInfo propertyInfo && !prop.Writable)
            {
                // enable private property setter deserialization for types with default constructor
                prop.Writable = propertyInfo.GetSetMethod(true) != null;
            }

            return prop;
        }
    }
}
