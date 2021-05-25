using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace myIsvService.Models
{
    public class ExternalConnection
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "state")]
        public ConnectionState ConnectionState { get; set; }

        [JsonProperty(PropertyName = "configuration")]
        public ExternalConnectionConfiguration Configuration { get; set; }

        public class ExternalConnectionConfiguration
        {
            public string[] AuthorizedApps { get; set; }
        }
    }
}
