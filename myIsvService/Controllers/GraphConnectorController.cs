using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Graph;
using Microsoft.Identity.Client;
using myIsvService.Models;
using myIsvService.Utilities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace myIsvService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class GraphConnectorController : ControllerBase
    {
        private string schemaErrorMessage = "Unable to find schema";
        private int maxAttempts = 6;
        private int numberOfMessages = 10;

        private readonly ILogger<GraphConnectorController> _logger;

        public GraphConnectorController(ILogger<GraphConnectorController> logger)
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
        [Route("{teamsAppId}")]
        public async Task<string> CreateConnetor([FromBody] ChangeNotificationCollection gcsNotification, [FromRoute] string teamsAppId)
        {
            _logger.LogInformation("starting Get");

            ChangeNotification changeNotification = gcsNotification.Value.First();
            _logger.LogInformation($"tenantId: {changeNotification.TenantId} ");
            string token = await GetToken(changeNotification, gcsNotification.ValidationTokens.First());
            _logger.LogInformation($"Token : {token}");

            ExternalConnectionBody externalConnectionBody = GetExternalConnectionBody(teamsAppId);

            await CreateConnection(externalConnectionBody, token);
            await CreateSchema(externalConnectionBody, token);

            _logger.LogInformation($"Scheduling item ingestion");
            Task.Run(() => ScheduleItemIngestion(externalConnectionBody, token, teamsAppId));
            return "";
        }

        private async Task CreateConnection(ExternalConnectionBody externalConnectionBody, string token)
        {
            _logger.LogInformation($"Creating connection for connecTIONID : {externalConnectionBody.id}");

            try
            {
                await Post("https://graph.microsoft.com/beta/external/connections", token, JsonConvert.SerializeObject(externalConnectionBody));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace);
                throw ex;
            }

            _logger.LogInformation($"Created connecTION with ID : {externalConnectionBody.id}. The connection may not yet be ready to receive data.");
        }

        private async Task CreateSchema(ExternalConnectionBody externalConnectionBody, string token)
        {
            _logger.LogInformation($"Creating schema for : {externalConnectionBody.id}");

            string schema = System.IO.File.ReadAllText(Path.Combine(System.IO.Directory.GetCurrentDirectory(), "schema.json"));
            _logger.LogInformation($"schema : {schema}");

            try
            {
                await Post($"https://graph.microsoft.com/beta/external/connections/{externalConnectionBody.id}/schema", token, schema);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace);
                throw ex;
            }

            _logger.LogInformation($"Sent request to create schema with ID : {externalConnectionBody.id}.");
        }

        private async Task ScheduleItemIngestion(ExternalConnectionBody externalConnectionBody, string token, string connectorId)
        {
            await CheckConnectionStatus(externalConnectionBody, token, connectorId);
            await CheckSchemaStatus(externalConnectionBody, token);
            await IngestItems(externalConnectionBody, token);
        }

        private async Task IngestItems(ExternalConnectionBody externalConnectionBody, string token)
        {
            ISVServiceUtilities.LoadData();
            for (int itemIndex = 0; itemIndex < numberOfMessages; itemIndex++)
            {
                try
                {
                    _logger.LogInformation($"Ingesting items for : {externalConnectionBody.id}.");

                    // Sleep for 1 second
                    Thread.Sleep(1000 * 1);
                    _logger.LogInformation($"Ingesting item number : {itemIndex + 1}.");

                    await IngestItem(externalConnectionBody, token);
                }
                catch (Exception e)
                {
                    _logger.LogError(e.Message + "\n" + e.StackTrace);
                }
            }

            _logger.LogInformation($"Finished batch ingesting items.");
        }

        private async Task IngestItem(ExternalConnectionBody externalConnectionBody, string token)
        {
            _logger.LogInformation($"writing object");

            try
            {
                Models.Item item = ISVServiceUtilities.GetItem();
                string body = JsonConvert.SerializeObject(item);
                await Put($"https://graph.microsoft.com/beta/external/connections/{externalConnectionBody.id}/items/{Guid.NewGuid()}", token, body.Replace("OdataType", "@odata.type"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace);
                throw ex;
            }


            _logger.LogInformation($"Done writing object for {externalConnectionBody.id}");
        }

        private async Task CheckConnectionStatus(ExternalConnectionBody externalConnectionBody, string token, string connectorId)
        {
            _logger.LogInformation($"Checking connection status for : {externalConnectionBody.id}.");
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    // sleep for 1 minute
                    Thread.Sleep(1000 * 60);
                    _logger.LogInformation($"Checking connection status. Attempt : {attempt + 1} out of : {maxAttempts}");

                    bool isConnectionReady = await GetConnection(externalConnectionBody, token, connectorId);

                    if (isConnectionReady)
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

        public async Task<bool> GetConnection(ExternalConnectionBody externalConnectionBody, string token, string connectorId)
        {
            _logger.LogInformation($"Getting external connection for : {externalConnectionBody.id}");

            try
            {
                var response = await Get($"https://graph.microsoft.com/beta/external/connections/{externalConnectionBody.id}", token);
                Microsoft.Graph.ExternalConnectors.ExternalConnection externalConnection = JsonConvert.DeserializeObject<Microsoft.Graph.ExternalConnectors.ExternalConnection>(response);
                return externalConnection.State != null && externalConnection.State.Equals(Microsoft.Graph.ExternalConnectors.ConnectionState.Ready);
            }
            catch (Exception e)
            {
                _logger.LogInformation($"unable to get connection for : {externalConnectionBody.id} because : {e.Message}");
                return false;
            }
        }

        private async Task CheckSchemaStatus(ExternalConnectionBody externalConnectionBody, string token)
        {
            _logger.LogInformation($"Checking the status of schema creation operation for : {externalConnectionBody.id}");
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                try
                {
                    _logger.LogInformation($"Checking schema attempt. Attempt : {attempt + 1} out of : {maxAttempts}");

                    var isSchemaCreated = await GetSchema(externalConnectionBody, token);

                    if (isSchemaCreated)
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

        private async Task<bool> GetSchema(ExternalConnectionBody externalConnectionBody, string token)
        {
            _logger.LogInformation($"Getting external connection for : {externalConnectionBody.id}");

            try
            {
                var response = await Get($"https://graph.microsoft.com/beta/external/connections/{externalConnectionBody.id}/schema", token);
                Microsoft.Graph.ExternalConnectors.Schema schema = JsonConvert.DeserializeObject<Microsoft.Graph.ExternalConnectors.Schema>(response);
                return schema != null && schema.Properties != null && schema.Properties.Count() == 3;
            }
            catch (Exception e)
            {
                _logger.LogInformation($"unable to get connection for : {externalConnectionBody.id} because : {e.Message}");
                return false;
            }
        }

        private async Task<string> Post(string url, string accessToken, string body)
        {
            using var client = new HttpClient();
            var data = new StringContent(body, Encoding.UTF8, "application/json");
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            _logger.LogInformation($"Sending POST request to {url} with body : {body}");
            var response = await client.PostAsync(url, data);

            if(!response.IsSuccessStatusCode)
            {
                throw new Exception(response.ReasonPhrase);
            }

            string result = response.Content.ReadAsStringAsync().Result;
            _logger.LogInformation(result);
            return result;
        }

        private async Task<string> Put(string url, string accessToken, string body)
        {
            using var client = new HttpClient();
            var data = new StringContent(body, Encoding.UTF8, "application/json");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            _logger.LogInformation($"Sending PUT request to {url} with body : {body}");
            var response = await client.PutAsync(url, data);
            string result = response.Content.ReadAsStringAsync().Result;
            _logger.LogInformation(result);
            return result;
        }

        private async Task<string> Get(string url, string accessToken)
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            var response = await client.GetAsync(url);
            string result = response.Content.ReadAsStringAsync().Result;
            _logger.LogInformation(result);
            return result;
        }

        private ExternalConnectionBody GetExternalConnectionBody(string teamsAppId)
        {
            ExternalConnectionBody externalConnectionBody = new ExternalConnectionBody();
            string externalConnectionId = Guid.NewGuid().ToString("N");
            _logger.LogInformation($"Generated connecTION Id : {externalConnectionId}");
            externalConnectionBody.id = externalConnectionId;
            externalConnectionBody.description = externalConnectionId + "_description";
            externalConnectionBody.name = externalConnectionId + "_name";
            externalConnectionBody.connectorId = teamsAppId;
            return externalConnectionBody;
        }

        private async Task<string> GetToken(ChangeNotification changeNotification, string jwtToken)
        {
            _logger.LogInformation("Getting access token. Reading Jwt token to get aadAppId and tenantId");

            var handler = new JwtSecurityTokenHandler();
            var token = handler.ReadJwtToken(jwtToken);

            string aadAppId = token.Audiences.FirstOrDefault();
            _logger.LogInformation($"aadAppId: {aadAppId}");

            string tenantId = changeNotification.TenantId.ToString();
            _logger.LogInformation($"TenantId: {tenantId}");

            string clientSecret = Environment.GetEnvironmentVariable(aadAppId);

            IConfidentialClientApplication confidentialClientApplication = ConfidentialClientApplicationBuilder
                    .Create(aadAppId)
                    .WithTenantId(tenantId)
                    .WithClientSecret(clientSecret)
                    .Build();

            string[] scopes = new string[] { "https://graph.microsoft.com/.default" };
            AuthenticationResult result = null;
            try
            {
                result = await confidentialClientApplication.AcquireTokenForClient(scopes)
                  .ExecuteAsync();
            }
            catch (MsalUiRequiredException ex)
            {
                // The application doesn't have sufficient permissions.
                // - Did you declare enough app permissions during app creation?
                // - Did the tenant admin grant permissions to the application?
                _logger.LogError(ex.StackTrace);
                throw ex;
            }
            catch (MsalServiceException ex) when (ex.Message.Contains("AADSTS70011"))
            {
                // Invalid scope. The scope has to be in the form "https://resourceurl/.default"
                // Mitigation: Change the scope to be as expected.
                _logger.LogError(ex.StackTrace);
                throw ex;
            }
            _logger.LogInformation("Fetched access token.");
            return result.AccessToken;
        }
    }
}
