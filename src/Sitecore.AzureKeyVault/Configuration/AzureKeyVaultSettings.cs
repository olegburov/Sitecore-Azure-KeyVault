﻿using Microsoft.Azure.KeyVault;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Sitecore.Abstractions;
using Sitecore.Diagnostics;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using System.Web.Configuration;

namespace Sitecore.Configuration
{
  public class AzureKeyVaultSettings : DefaultSettings
  {
    private readonly ConcurrentDictionary<string, string> decryptedSecrets;

    protected KeyVaultClient Client { get; set; }

    public AzureKeyVaultSettings(BaseFactory factory, BaseLog log) : base(factory, log)
    {
      this.decryptedSecrets = new ConcurrentDictionary<string, string>();
      this.Client = new KeyVaultClient(new KeyVaultClient.AuthenticationCallback(this.GetToken));
    }

    public override string GetConnectionString(string connectionStringName)
    {
      Assert.ArgumentNotNullOrEmpty(connectionStringName, nameof(connectionStringName));

      var connectionString = string.Empty;

      if (this.decryptedSecrets.ContainsKey(connectionStringName))
      {
        connectionString = this.decryptedSecrets[connectionStringName];
      }
      else
      {
        var setting = WebConfigurationManager.ConnectionStrings[connectionStringName];
        Assert.IsNotNull(setting, "Unknown connection string. Name: '{0}'", connectionStringName);

        connectionString = setting.ConnectionString;
        Assert.IsNotNullOrEmpty(connectionString, "Connection string is empty. Name: '{0}'", connectionStringName);

        if (this.IsSecretIdentifier(connectionString))
        {
          var secret = this.Client.GetSecretAsync(connectionString).Result;
          connectionString = secret.Value;
          this.decryptedSecrets.TryAdd(connectionStringName, connectionString);
        }        
      }

      return connectionString;
    }

    protected async Task<string> GetToken(string authority, string resource, string scope)
    {
      var authContext = new AuthenticationContext(authority);

      var clientCred = new ClientCredential(
        WebConfigurationManager.AppSettings["ClientId"],
        WebConfigurationManager.AppSettings["ClientSecret"]);

      var result = await authContext.AcquireTokenAsync(resource, clientCred);

      if (result == null)
      {
        throw new InvalidOperationException("Failed to obtain the JWT token");
      }

      return result.AccessToken;
    }

    protected bool IsSecretIdentifier(string ConnectionString)
    {
      return ConnectionString.Contains(".vault.azure.net");
    }
  }
}