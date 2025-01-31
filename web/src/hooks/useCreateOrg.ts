// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

import { useHistory } from 'react-router-dom';
import { useMutation, useQueryClient } from 'react-query'
import { DeploymentScopeDefinition, OrganizationDefinition, ProjectTemplateDefinition } from 'teamcloud';
import { api } from '../API';

export const useCreateOrg = () => {

    const history = useHistory();
    const queryClient = useQueryClient();

    return useMutation(async (def: { orgDef: OrganizationDefinition, scopeDef?: DeploymentScopeDefinition, templateDef?: ProjectTemplateDefinition }) => {

        console.log(`- createOrg`);
        const orgResponse = await api.createOrganization({ body: def.orgDef });
        const newOrg = orgResponse.data;
        console.log(`+ createOrg`);

        let scope, template;

        if (newOrg?.id) {
            if (def.scopeDef) {
                console.log(`- createDeploymentScope`);
                const scopeResponse = await api.createDeploymentScope(newOrg.id, { body: def.scopeDef });
                scope = scopeResponse.data;
                console.log(`+ createDeploymentScope`);
            }
            if (def.templateDef) {
                console.log(`- createProjectTemplate`);
                const templateResponse = await api.createProjectTemplate(newOrg.id, { body: def.templateDef });
                template = templateResponse.data;
                console.log(`+ createProjectTemplate`);
            }
        }

        return { org: newOrg, scope: scope, template: template }

    }, {
        onSuccess: data => {
            if (data.org) {

                queryClient.invalidateQueries('orgs');

                if (data.scope) {
                    console.log(`+ setDeploymentScopes (${data.org.slug})`);
                    queryClient.setQueryData(['org', data.org.id, 'scopes'], [data.scope])
                }

                if (data.template) {
                    console.log(`+ setProjectTemplates (${data.org.slug})`);
                    queryClient.setQueryData(['org', data.org.id, 'templates'], [data.template])
                }

                console.log(`+ setOrg (${data.org.slug})`);
                queryClient.setQueryData(['org', data.org.slug], data.org)

                history.push(`/orgs/${data.org.slug}`);
            }
        }
    }).mutateAsync
}
