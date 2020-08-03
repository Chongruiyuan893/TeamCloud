/**
 *  Copyright (c) Microsoft Corporation.
 *  Licensed under the MIT License.
 */

using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TeamCloud.Model.Internal.Data
{
    internal static class PropertyCache
    {
        internal static readonly ConcurrentDictionary<Type, IEnumerable<PropertyInfo>> Properties = new ConcurrentDictionary<Type, IEnumerable<PropertyInfo>>();
    }

    public interface IPopulate<T>
        where T : class, new()
    {
        void PopulateFromExternalModel(T source)
        {
            var inerfaces = GetType().GetInterfaces().Intersect(GetType().GetInterfaces());

            var sourceProperties = PropertyCache.Properties.GetOrAdd(typeof(T), t => t.GetProperties());

            var targetProperties = PropertyCache.Properties.GetOrAdd(GetType(), t => t.GetProperties());

            foreach (var sourceProperty in sourceProperties)
            {
                var targetProperty = targetProperties.SingleOrDefault(p => p.Name == sourceProperty.Name);

                if (targetProperty?.PropertyType == sourceProperty.PropertyType)
                {
                    var sourceValue = sourceProperty.GetValue(source);

                    targetProperty.SetValue(this, sourceValue);

                    continue;
                }

                if (!(targetProperty is null) && !(sourceProperty.GetValue(source) is null))
                {
                    if (typeof(IEnumerable).IsAssignableFrom(sourceProperty.PropertyType) && typeof(IEnumerable).IsAssignableFrom(targetProperty.PropertyType))
                    {
                        var targetType = targetProperty.PropertyType
                            .GetInterfaces()
                            .FirstOrDefault(i => i.IsGenericType && typeof(IEnumerable).IsAssignableFrom(i))
                            .GetGenericArguments()
                            .SingleOrDefault();

                        var sourceType = sourceProperty.PropertyType
                            .GetInterfaces()
                            .FirstOrDefault(i => i.IsGenericType && typeof(IEnumerable).IsAssignableFrom(i))
                            .GetGenericArguments()
                            .SingleOrDefault();

                        var populateType = typeof(IPopulate<>).MakeGenericType(sourceType);

                        var populateMethod = populateType.GetMethod(nameof(PopulateFromExternalModel));

                        if (populateType.IsAssignableFrom(targetType))
                        {
                            var sourceEnumeration = (IEnumerable)sourceProperty.GetValue(source);

                            var targetItems = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(targetType));

                            foreach (var sourceItem in sourceEnumeration)
                            {
                                var targetItem = Activator.CreateInstance(targetType);

                                populateMethod.Invoke(targetItem, new[] { sourceItem });

                                targetItems.Add(targetItem);
                            }

                            targetProperty.SetValue(this, targetItems);
                        }
                    }
                    else
                    {
                        var populateInterfaceType = typeof(IPopulate<>).MakeGenericType(sourceProperty.PropertyType);

                        var populateInterfaceMethod = populateInterfaceType.GetMethod(nameof(PopulateFromExternalModel));

                        if (populateInterfaceType.IsAssignableFrom(targetProperty.PropertyType))
                        {
                            var sourceValue = sourceProperty.GetValue(source);

                            var targetValue = targetProperty.GetValue(this) ?? Activator.CreateInstance(targetProperty.PropertyType);

                            populateInterfaceMethod.Invoke(targetValue, new[] { sourceValue });

                            targetProperty.SetValue(this, targetValue);
                        }
                    }
                }
            }
        }

        T PopulateExternalModel(T target = null)
        {
            target ??= Activator.CreateInstance<T>();

            var inerfaces = typeof(T).GetInterfaces().Intersect(GetType().GetInterfaces());

            var sourceProperties = PropertyCache.Properties.GetOrAdd(GetType(), t => t.GetProperties());

            var targetProperties = PropertyCache.Properties.GetOrAdd(typeof(T), t => t.GetProperties());

            foreach (var sourceProperty in sourceProperties)
            {
                var targetProperty = targetProperties.SingleOrDefault(p => p.Name == sourceProperty.Name);

                if (targetProperty?.PropertyType == sourceProperty.PropertyType)
                {
                    var sourceValue = sourceProperty.GetValue(this);

                    targetProperty.SetValue(target, sourceValue);

                    continue;
                }

                if (!(targetProperty is null) && !(sourceProperty.GetValue(this) is null))
                {
                    if (typeof(IEnumerable).IsAssignableFrom(sourceProperty.PropertyType) && typeof(IEnumerable).IsAssignableFrom(targetProperty.PropertyType))
                    {
                        var targetType = targetProperty.PropertyType
                            .GetInterfaces()
                            .FirstOrDefault(i => i.IsGenericType && typeof(IEnumerable).IsAssignableFrom(i))
                            .GetGenericArguments()
                            .SingleOrDefault();

                        var sourceType = sourceProperty.PropertyType
                            .GetInterfaces()
                            .FirstOrDefault(i => i.IsGenericType && typeof(IEnumerable).IsAssignableFrom(i))
                            .GetGenericArguments()
                            .SingleOrDefault();

                        var populateType = typeof(IPopulate<>).MakeGenericType(targetType);

                        var populateMethod = populateType.GetMethod(nameof(PopulateExternalModel));

                        if (populateType.IsAssignableFrom(sourceType))
                        {
                            var sourceEnumeration = (IEnumerable)sourceProperty.GetValue(this);

                            var targetItems = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(targetType));

                            foreach (var sourceItem in sourceEnumeration)
                            {
                                var targetItem = Activator.CreateInstance(targetType);

                                populateMethod.Invoke(sourceItem, new[] { targetItem });

                                targetItems.Add(targetItem);
                            }

                            targetProperty.SetValue(target, targetItems);
                        }
                    }
                    else
                    {
                        var populateInterfaceType = typeof(IPopulate<>).MakeGenericType(targetProperty.PropertyType);

                        var populateInterfaceMethod = populateInterfaceType.GetMethod(nameof(PopulateExternalModel));

                        if (populateInterfaceType.IsAssignableFrom(sourceProperty.PropertyType))
                        {
                            var sourceValue = sourceProperty.GetValue(this);

                            var targetValue = targetProperty.GetValue(target) ?? Activator.CreateInstance(targetProperty.PropertyType);

                            populateInterfaceMethod.Invoke(sourceValue, new[] { targetValue });

                            targetProperty.SetValue(target, targetValue);
                        }
                    }
                }
            }

            return target;
        }
    }

    public static class PopulateExtensions
    {
        public static TExternal PopulateExternalModel<TInternal, TExternal>(this TInternal source, TExternal target = null)
            where TInternal : IPopulate<TExternal>
            where TExternal : class, new()
            => source.PopulateExternalModel(target);

        public static Model.Data.User PopulateExternalModel(this UserDocument source, Model.Data.User target = null)
            => source.PopulateExternalModel<UserDocument, Model.Data.User>(target);

        public static Model.Data.User PopulateExternalModel(this UserDocument source, string projectId, Model.Data.User target = null)
        {
            var user = source.PopulateExternalModel<UserDocument, Model.Data.User>(target);
            user.ProjectMemberships = user.ProjectMemberships.TakeWhile(m => m.ProjectId == projectId).ToList();
            return user;
        }

        public static Model.Data.Project PopulateExternalModel(this ProjectDocument source, Model.Data.Project target = null)
        {
            var project = source.PopulateExternalModel<ProjectDocument, Model.Data.Project>(target);
            foreach (var user in project.Users)
                user.ProjectMemberships = user.ProjectMemberships.TakeWhile(m => m.ProjectId == project.Id).ToList();
            return project;
        }

        public static Model.Data.Provider PopulateExternalModel(this ProviderDocument source, Model.Data.Provider target = null)
            => source.PopulateExternalModel<ProviderDocument, Model.Data.Provider>(target);

        public static Model.Data.ProjectType PopulateExternalModel(this ProjectTypeDocument source, Model.Data.ProjectType target = null)
            => source.PopulateExternalModel<ProjectTypeDocument, Model.Data.ProjectType>(target);

        public static Model.Data.TeamCloudInstance PopulateExternalModel(this TeamCloudInstanceDocument source, Model.Data.TeamCloudInstance target = null)
            => source.PopulateExternalModel<TeamCloudInstanceDocument, Model.Data.TeamCloudInstance>(target);

        public static void PopulateFromExternalModel<TInternal, TExternal>(this TInternal target, TExternal source)
            where TInternal : IPopulate<TExternal>
            where TExternal : class, new()
            => target.PopulateFromExternalModel(source);
    }
}
