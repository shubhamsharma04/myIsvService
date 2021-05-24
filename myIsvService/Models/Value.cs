using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace myIsvService.Models
{
    public class Value
    {
        public string Id { get; set; }
        
        public string changeType { get; set; }

        public string Resource { get; set; }

        public string tenantId { get; set; }
    }
}
