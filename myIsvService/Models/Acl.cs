using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace myIsvService.Models
{
    public class Acl
    {
        public string type { get; set; }

        public string value { get; set; }

        public string accessType { get; set; }

        public string identitySource { get; set; }
    }
}
