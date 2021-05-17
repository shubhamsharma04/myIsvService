using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private string clientId = "5002759b-3b93-4c7e-bdc1-48a4b2842404";
        private string tenantId = "d8cbc5c5-e484-48ea-af80-fc4083a2a740";
        private string userId = "cbb6d774-d245-4927-9a4f-eea22c3f7ff4";
        private static string clientSecret = Environment.GetEnvironmentVariable(nameof(clientSecret));

        private GraphServiceClient graphClient;
        private readonly ILogger<IsvServiceController> _logger;

        public IsvServiceController(ILogger<IsvServiceController> logger)
        {
            graphClient = GetGraphClient();
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
            await CreateConnection(externalConnectionId);
            await CreateSchema(externalConnectionId);
            return "";
        }

        [HttpPut]
        [Route("{externalConnectionId}")]
        public async Task<string> CreateItem([FromRoute] string externalConnectionId)
        {
            _logger.LogInformation("Ingesting item");
            await IngestItem(externalConnectionId);
            return "";
        }

        [HttpGet]
        [Route("GetAllConnections")]
        public async Task<string> GetAllConnections()
        {
            _logger.LogInformation("Getting all connections");

            var connections = await graphClient.External.Connections
                                                .Request()
                                                .GetAsync();
            StringBuilder response = new StringBuilder();

            foreach(Microsoft.Graph.ExternalConnectors.ExternalConnection connection in connections)
            {
                response.Append($"{{ ConnectionName : {connection.Id} ConnectionState : {connection.State} Schema : {connection.Schema?.Id} }}");
                response.Append("\n");
            }

            return response.ToString();
        }

        [HttpGet]
        [Route("GetConnection/{externalConnectionId}")]
        public async Task<string> GetConnection([FromRoute] string externalConnectionId)
        {
            _logger.LogInformation($"Getting external connection for : {externalConnectionId}");

            var connection = await graphClient.External.Connections[externalConnectionId]
                                                .Request()
                                                .GetAsync();
            return $"{{ ConnectionName : {connection.Id} ConnectionState : {connection.State} Schema : {connection.Schema?.Id} }}";
        }

        [HttpGet]
        [Route("GetSchema/{externalConnectionId}")]
        public async Task<string> GetSchema([FromRoute] string externalConnectionId)
        {
            _logger.LogInformation($"Getting schema for : {externalConnectionId}");

            var schema = await graphClient.External.Connections[externalConnectionId].Schema
                                                .Request()
                                                .GetAsync();
            var response = new StringBuilder("Schema\n");
            if(schema != null)
            {
                foreach(Microsoft.Graph.ExternalConnectors.Property property in schema.Properties)
                {
                    response.Append(property.Name + " : " + property.Type).Append("\n");
                }
            }
            return response.ToString();
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

        private async Task CreateConnection(string externalConnectionId)
        {
            _logger.LogInformation($"Creating connection for : {externalConnectionId}");

            var externalConnection = new Microsoft.Graph.ExternalConnectors.ExternalConnection
            {
                Id = externalConnectionId,
                Name = externalConnectionId,
                Description = "Connection to upload data",
                ODataType = null
            };

            try
            {
                var response = await graphClient.External.Connections
                                                .Request()
                                                .AddAsync(externalConnection);
            } catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace);
                throw ex;
            }
            
            _logger.LogInformation($"Created connection with ID : {externalConnectionId}");
        }

        private async Task CreateSchema(string externalConnectionId)
        {
            _logger.LogInformation($"Creating schema for : {externalConnectionId}");
            var schema = new Microsoft.Graph.ExternalConnectors.Schema
            {
                BaseType = "microsoft.graph.externalItem",
                ODataType = null,
                Properties = new List<Microsoft.Graph.ExternalConnectors.Property>()
                {
                    new Microsoft.Graph.ExternalConnectors.Property
                    {
                        Name = "Question",
                        Type = Microsoft.Graph.ExternalConnectors.PropertyType.String,
                        IsSearchable = true,
                        IsRetrievable = true,
                        Labels = new List<Microsoft.Graph.ExternalConnectors.Label>()
                        {
                            Microsoft.Graph.ExternalConnectors.Label.Title
                        },
                        ODataType = null
                    },
                    new Microsoft.Graph.ExternalConnectors.Property
                    {
                        Name = "Answer",
                        Type = Microsoft.Graph.ExternalConnectors.PropertyType.String,
                        IsSearchable = true,
                        IsRetrievable = true,
                        ODataType = null
                    },
                    new Microsoft.Graph.ExternalConnectors.Property
                    {
                        Name = "userName",
                        Type = Microsoft.Graph.ExternalConnectors.PropertyType.String,
                        IsQueryable = true,
                        IsRetrievable = true,
                        IsSearchable = true,
                        ODataType = null
                    }
                }
            };

            Microsoft.Graph.ExternalConnectors.Schema response = null;
            try
            {
                response = await graphClient.External.Connections[externalConnectionId].Schema
                                                .Request()
                                                .Header("Prefer", "respond-async")
                                                .CreateAsync(schema);
                _logger.LogInformation(response?.Id);
            } catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace);
                throw ex;
            }
            

            _logger.LogInformation($"Sent request to create schema with ID.");
        } 

        private async Task IngestItem(string externalConnectionId)
        {
            _logger.LogInformation($"writing object for {externalConnectionId}");

            List<Microsoft.Graph.ExternalConnectors.Acl> acls = GetAcls();
            Microsoft.Graph.ExternalConnectors.Properties properties = new Microsoft.Graph.ExternalConnectors.Properties
            {
                AdditionalData = new Dictionary<string, object>()
                {
                    {"userTitle", "CEO"},
                    {"userName", "ISV Name"},
                    {"userId", "ISV ID"}
                }
            };

            var content = new Microsoft.Graph.ExternalConnectors.ExternalItemContent
            {
                Value = "CEO",
                Type = Microsoft.Graph.ExternalConnectors.ExternalItemContentType.Text,
                ODataType = null
            };

            Microsoft.Graph.ExternalConnectors.ExternalItem externalItem = new Microsoft.Graph.ExternalConnectors.ExternalItem
            {
                Acl = acls,
                Properties = properties,
                Content = content
            };

            try
            {
                var response = await graphClient.External.Connections[externalConnectionId].Items["TSP228082938"]
                                                .Request()
                                                .PutAsync(externalItem);
                _logger.LogInformation(response?.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace);
                throw ex;
            }
            

            _logger.LogInformation($"Done writing object for {externalConnectionId}");
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
                },
                new Microsoft.Graph.ExternalConnectors.Acl
                {
                    Type = Microsoft.Graph.ExternalConnectors.AclType.User,
                    Value = userId,
                    AccessType = Microsoft.Graph.ExternalConnectors.AccessType.Grant,
                    IdentitySource = Microsoft.Graph.ExternalConnectors.IdentitySourceType.AzureActiveDirectory
                }
            };
        }
    }
}
