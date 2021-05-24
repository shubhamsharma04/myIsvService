using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Graph.Auth;
using Microsoft.Identity.Client;
using myIsvService.Utilities;

namespace myIsvService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class IsvServiceController : ControllerBase
    {
        private string schemaErrorMessage = "Unable to find schema";
        private int maxAttempts = 6;
        private int numberOfMessages = 4;
        private static string clientId = Environment.GetEnvironmentVariable(nameof(clientId));
        private static string tenantId = Environment.GetEnvironmentVariable(nameof(tenantId));
        private static string userId = Environment.GetEnvironmentVariable(nameof(userId));
        private static string clientSecret = Environment.GetEnvironmentVariable(nameof(clientSecret));

        private GraphServiceClient graphClient;
        private readonly ILogger<IsvServiceController> _logger;

        public IsvServiceController(ILogger<IsvServiceController> logger)
        {
            graphClient = GetGraphClient();
            ISVServiceUtilities.LoadData();
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
        public async Task<string> CreateConnectionAndSchema([FromRoute] string externalConnectionId)
        {
            _logger.LogInformation("starting creating connection and schema.");
            externalConnectionId = externalConnectionId.Replace("-", string.Empty);
            if(externalConnectionId.Length > 32)
            {
                throw new ArgumentException($"The length of connection ID cannot be greater than 32. Length of connectionID : {externalConnectionId.Length}.");
            }

            await CreateConnection(externalConnectionId);
            await CreateSchema(externalConnectionId);

            _logger.LogInformation($"Scheduling item ingestion");
            Task.Run(() => ScheduleItemIngestion(externalConnectionId));
            return "";
        }

        [HttpPut]
        [Route("{externalConnectionId}")]
        public async Task<string> CreateItem([FromRoute] string externalConnectionId)
        {
            _logger.LogInformation($"Ingesting item for connection with ID: {externalConnectionId}");
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

            foreach (Microsoft.Graph.ExternalConnectors.ExternalConnection connection in connections)
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
            Microsoft.Graph.ExternalConnectors.ExternalConnection connection = null;
            try
            {
                connection = await graphClient.External.Connections[externalConnectionId]
                                                .Request()
                                                .GetAsync();
            }
            catch (Exception e)
            {
                _logger.LogInformation($"unable to get connection for : {externalConnectionId} because : {e.Message}");
                return "";
            }

            return connection.State?.ToString();
        }

        [HttpGet]
        [Route("GetSchema/{externalConnectionId}")]
        public async Task<string> GetSchema([FromRoute] string externalConnectionId)
        {
            _logger.LogInformation($"Getting schema for : {externalConnectionId}");

            Microsoft.Graph.ExternalConnectors.Schema schema = null;
            try
            {
                schema = await graphClient.External.Connections[externalConnectionId].Schema
                                                .Request()
                                                .GetAsync();
            }
            catch (Exception e)
            {
                _logger.LogInformation($"unable to find schema for : {externalConnectionId} because : {e.Message}");
                return schemaErrorMessage;
            }

            var response = new StringBuilder();
            if (schema != null)
            {
                response.Append("Schema\n");
                foreach (Microsoft.Graph.ExternalConnectors.Property property in schema.Properties)
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace);
                throw ex;
            }

            _logger.LogInformation($"Created connection with ID : {externalConnectionId}. The connection may not yet be ready to receive data.");
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
                        Name = "UserName",
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
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace);
                throw ex;
            }

            _logger.LogInformation($"Sent request to create schema with ID : {externalConnectionId}.");
        }

        private async Task ScheduleItemIngestion(string externalConnectionId)
        {
            await CheckConnectionStatus(externalConnectionId);
            await CheckSchemaStatus(externalConnectionId);
            await IngestItems(externalConnectionId);
        }

        private async Task CheckConnectionStatus(string externalConnectionId)
        {
            _logger.LogInformation($"Checking connection status for : {externalConnectionId}.");
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    // sleep for 1 minute
                    Thread.Sleep(1000 * 60);
                    _logger.LogInformation($"Checking connection status. Attempt : {attempt + 1} out of : {maxAttempts}");

                    var connectionStatus = await GetConnection(externalConnectionId);

                    if (!string.IsNullOrWhiteSpace(connectionStatus) && string.Equals("Ready", connectionStatus, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation($"Connection created");
                        return;
                    }
                    _logger.LogInformation($"Connection not ready yet.");
                }
                catch (Exception e)
                {
                    _logger.LogError(e.Message + "\n" + e.StackTrace);
                }

            }

            throw new Exception("Unable to create connection");
        }

        private async Task CheckSchemaStatus(string externalConnectionId)
        {
            _logger.LogInformation($"Checking the status of schema creation operation for : {externalConnectionId}");
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    _logger.LogInformation($"Checking schema attempt. Attempt : {attempt + 1} out of : {maxAttempts}");

                    var schemaStatus = await GetSchema(externalConnectionId);

                    if (!string.IsNullOrWhiteSpace(schemaStatus))
                    {
                        _logger.LogInformation($"Schema created");
                        return;
                    }
                    _logger.LogInformation($"Schema not created yet.");

                    // sleep for 20 seconds
                    Thread.Sleep(1000 * 20);
                }
                catch (Exception e)
                {
                    _logger.LogError(e.Message + "\n" + e.StackTrace);
                }
            }

            throw new Exception("Unable to create schema");
        }

        private async Task IngestItems(string externalConnectionId)
        {
            for (int itemIndex = 0; itemIndex < numberOfMessages; itemIndex++)
            {
                try
                {
                    _logger.LogInformation($"Ingesting items for : {externalConnectionId}.");

                    // Sleep for 3 seconds
                    Thread.Sleep(1000 * 3);
                    _logger.LogInformation($"Ingesting item number : {itemIndex + 1}.");

                    await IngestItem(externalConnectionId);
                }
                catch (Exception e)
                {
                    _logger.LogError(e.Message + "\n" + e.StackTrace);
                }
            }

            _logger.LogInformation($"Finished batch ingesting items.");
        }

        private async Task IngestItem(string externalConnectionId)
        {
            _logger.LogInformation($"writing object");

            List<Microsoft.Graph.ExternalConnectors.Acl> acls = GetAcls();
            IDictionary<string, object> additionalData = ISVServiceUtilities.GetAdditionalData();
            Microsoft.Graph.ExternalConnectors.Properties properties = new Microsoft.Graph.ExternalConnectors.Properties
            {
                AdditionalData = additionalData
            };

            var contentData = ISVServiceUtilities.GetContentData(additionalData);
            _logger.LogInformation($"ContentData : {contentData}");

            var content = new Microsoft.Graph.ExternalConnectors.ExternalItemContent
            {
                Value = contentData,
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
                var response = await graphClient.External.Connections[externalConnectionId].Items[Guid.NewGuid().ToString()]
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
