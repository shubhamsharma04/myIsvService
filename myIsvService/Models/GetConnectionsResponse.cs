using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace myIsvService.Models
{
    public class GetConnectionsResponse
    {
        [JsonProperty(PropertyName = "value")]
        public ExternalConnection[] Value { get; set; }
    }
}
