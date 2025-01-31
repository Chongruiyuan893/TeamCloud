﻿/**
 *  Copyright (c) Microsoft Corporation.
 *  Licensed under the MIT License.
 */

using System;
using System.Threading.Tasks;
using TeamCloud.Model.Data.Core;

namespace TeamCloud.Data
{
    public abstract class DocumentExpander : IDocumentExpander
    {
        public virtual bool CanExpand(IContainerDocument document)
        {
            if (document is null)
                throw new ArgumentNullException(nameof(document));

            return typeof(IDocumentExpander<>)
                .MakeGenericType(document.GetType())
                .IsAssignableFrom(GetType());
        }

        public virtual async Task<IContainerDocument> ExpandAsync(IContainerDocument document)
        {
            if (document is null)
                throw new ArgumentNullException(nameof(document));

            if (CanExpand(document))
            {
                var expandMethod = typeof(IDocumentExpander<>)
                    .MakeGenericType(document.GetType())
                    .GetMethod(nameof(ExpandAsync), new Type[] { document.GetType() });

                var expandTask = (Task)expandMethod.Invoke(this, new object[] { document });

                await expandTask.ConfigureAwait(false);

                return (IContainerDocument)expandTask.GetType()
                    .GetProperty(nameof(Task<object>.Result))
                    .GetValue(expandTask);
            }

            throw new NotImplementedException($"Missing document expander implementation IDocumentExpander<{document.GetType().Name}> at {GetType()}");
        }
    }

    public interface IDocumentExpander
    {
        bool CanExpand(IContainerDocument document);

        Task<IContainerDocument> ExpandAsync(IContainerDocument document);
    }

    public interface IDocumentExpander<T> : IDocumentExpander
        where T : class, IContainerDocument, new()
    {
        public Task<T> ExpandAsync(T document);
    }
}
