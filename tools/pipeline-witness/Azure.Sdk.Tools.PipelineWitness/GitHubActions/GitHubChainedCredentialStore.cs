using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Octokit;

namespace Azure.Sdk.Tools.PipelineWitness.GitHubActions;

public class GitHubChainedCredentialStore : ICredentialStore
{
    private readonly ICredentialStore[] storeChain;
    private ICredentialStore lastSuccessfulStore;

    public GitHubChainedCredentialStore(params ICredentialStore[] storeChain)
    {
        this.storeChain = storeChain;
    }

    public async Task<Credentials> GetCredentials()
    {
        var innerExceptions = new List<Exception>();

        if (this.lastSuccessfulStore != null)
        {
            try
            {
                return await this.lastSuccessfulStore.GetCredentials();
            }
            catch (Exception ex)
            {
                innerExceptions.Add(ex);
            }
        }

        foreach (var store in this.storeChain)
        {
            if (store == this.lastSuccessfulStore)
                continue;

            try
            {
                var credentials = await store.GetCredentials();
                this.lastSuccessfulStore = store;
                return credentials;
            }
            catch (Exception ex)
            {
                innerExceptions.Add(ex);
            }
        }

        this.lastSuccessfulStore = null;
        throw new AggregateException("Unable to get credentials from any store", innerExceptions);
    }
}
