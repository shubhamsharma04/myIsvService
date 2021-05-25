using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace myIsvService.Models
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ConnectionState
    {
        [EnumMember(Value = "draft")]
        Draft,

        [EnumMember(Value = "ready")]
        Ready,

        [EnumMember(Value = "obsolete")]
        Obsolete,

        [EnumMember(Value = "limitExceeded")]
        LimitExceeded,
    }
}
