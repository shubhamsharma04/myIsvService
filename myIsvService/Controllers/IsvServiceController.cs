using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Auth;
using Microsoft.Identity.Client;

namespace myIsvService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class IsvServiceController : ControllerBase
    {
        private string clientId = "5c6fef57-2614-4e2f-a0ff-08687c7f4c51";
        private string tenantId = "c59d6ef9-7063-4f77-9089-99d59ac78f35";
        private static string clientSecret = Environment.GetEnvironmentVariable(nameof(clientSecret));

        private readonly ILogger<IsvServiceController> _logger;

        public IsvServiceController(ILogger<IsvServiceController> logger)
        {
            _logger = logger;
        }

        [HttpGet]
        public string Get()
        {
            _logger.LogInformation("starting Get");
            return "";
        }

        [HttpPost]
        [Route("{externalConnectionId}")]
        public async Task<string> CreateConnectionAndSchema([FromRoute]string externalConnectionId)
        {
            _logger.LogInformation("starting creating connection and schema.");
            GraphServiceClient graphClient = GetGraphClient();
            await CreateConnection(graphClient, externalConnectionId);
            await CreateSchema(graphClient, externalConnectionId);
            return "";
        }

        [HttpPut]
        [Route("{externalConnectionId}")]
        public async Task<string> CreateItem([FromRoute] string externalConnectionId)
        {
            _logger.LogInformation("Ingesting item");
            GraphServiceClient graphClient = GetGraphClient();
            await IngestItem(graphClient, externalConnectionId);
            return "";
        }

        private GraphServiceClient GetGraphClient()
        {
            IConfidentialClientApplication confidentialClientApplication = ConfidentialClientApplicationBuilder
                .Create(clientId)
                .WithTenantId(tenantId)
                .WithClientSecret(clientSecret)
                .Build();

            ClientCredentialProvider authProvider = new ClientCredentialProvider(confidentialClientApplication);
            return new GraphServiceClient(authProvider);
        }

        private async Task CreateConnection(GraphServiceClient graphClient, string externalConnectionId)
        {
            _logger.LogInformation($"Creating connection for : {externalConnectionId}");

            var externalConnection = new Microsoft.Graph.ExternalConnectors.ExternalConnection
            {
                Id = externalConnectionId,
                Name = "myIsvServiceId connection",
                Description = "Connection to upload data",
                ODataType = null
            };

            var response = await graphClient.External.Connections
            .Request()
            .AddAsync(externalConnection);

            _logger.LogInformation($"Created connection with ID : {response.Id}");
        }

        private async Task CreateSchema(GraphServiceClient graphClient, string externalConnectionId)
        {
            _logger.LogInformation($"Creating schema for : {externalConnectionId}");
            var schema = new Microsoft.Graph.ExternalConnectors.Schema
            {
                BaseType = "microsoft.graph.externalItem",
                Properties = new List<Microsoft.Graph.ExternalConnectors.Property>()
                {
                    new Microsoft.Graph.ExternalConnectors.Property
                    {
                        Name = "userName",
                        Type = Microsoft.Graph.ExternalConnectors.PropertyType.String,
                        IsSearchable = true,
                        IsRetrievable = true,
                        IsQueryable = true
                    },
                    new Microsoft.Graph.ExternalConnectors.Property
                    {
                        Name = "userId",
                        Type = Microsoft.Graph.ExternalConnectors.PropertyType.String,
                        IsQueryable = true,
                        IsRetrievable = true,
                        IsSearchable = false
                    }
                }
            };

            var response = await graphClient.External.Connections[externalConnectionId].Schema
            .Request()
            .CreateAsync(schema);

            _logger.LogInformation($"Sent request to create schema with ID.");
        }

        private List<Microsoft.Graph.ExternalConnectors.Acl> GetAcls()
        {
            return new List<Microsoft.Graph.ExternalConnectors.Acl>
            {
                new Microsoft.Graph.ExternalConnectors.Acl
                {
                    Type = Microsoft.Graph.ExternalConnectors.AclType.User,
                    Value = clientId,
                    AccessType = Microsoft.Graph.ExternalConnectors.AccessType.Grant,
                    IdentitySource = Microsoft.Graph.ExternalConnectors.IdentitySourceType.AzureActiveDirectory
                }
            };
        }

        private async Task IngestItem(GraphServiceClient graphClient, string externalConnectionId)
        {
            _logger.LogInformation($"writing object for {externalConnectionId}");

            List<Microsoft.Graph.ExternalConnectors.Acl> acls = GetAcls();
            Microsoft.Graph.ExternalConnectors.Properties properties = new Microsoft.Graph.ExternalConnectors.Properties
            {
                AdditionalData = new Dictionary<string, object>()
                {
                    {"userName", "isvUserName"},
                    {"userId", "1"}
                }
            };

            Microsoft.Graph.ExternalConnectors.ExternalItem externalItem = new Microsoft.Graph.ExternalConnectors.ExternalItem
            {
                Acl = acls,
                Properties = properties
            };

            var response = await graphClient.External.Connections[externalConnectionId].Items[Guid.NewGuid().ToString()]
                        .Request()
                        .PutAsync(externalItem);

            _logger.LogInformation($"Done writing object for {externalConnectionId}");
        }
    }
}
