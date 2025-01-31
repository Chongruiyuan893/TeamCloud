// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import { useParams } from 'react-router-dom';
import { useQuery } from 'react-query'
import { useIsAuthenticated } from '@azure/msal-react';
import { api } from '../API';

export const useOrg = () => {

    const { orgId } = useParams() as { orgId: string };

    const isAuthenticated = useIsAuthenticated();

    return useQuery(['org', orgId], async () => {
        const { data } = await api.getOrganization(orgId);
        return data;
    }, {
        enabled: isAuthenticated && !!orgId
    });
}
