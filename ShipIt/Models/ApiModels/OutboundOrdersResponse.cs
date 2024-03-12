using System.Collections.Generic;
using System.Net.Http.Headers;

namespace ShipIt.Models.ApiModels
{
    public class OutboundOrdersResponse: Response
    {
        public List<Truck> Trucks {get;set;} = [];
    }
}