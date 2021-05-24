using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace myIsvService.Models
{
    public class Item
    {
        public string OdataType = "microsoft.graph.externalItem";

        public List<Acl> acl { get; set; }

        public Properties properties { get; set; }

        public Content content { get; set; }
    }
}
