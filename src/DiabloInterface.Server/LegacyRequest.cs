using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Zutatensuppe.DiabloInterface.Server
{
    public class LegacyRequest
    {
        public string EquipmentSlot { get; set; }

        public Request AsRequest()
        {
            var request = new Request();
            request.Resource = $@"items/{EquipmentSlot}";
            return request;
        }
    }
}
