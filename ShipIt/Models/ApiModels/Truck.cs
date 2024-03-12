using System.Collections.Generic;

namespace ShipIt.Models.ApiModels
{
    public class Truck
    {
        public int Id { get; set; }
        public double Weight { get; set; } = 0.0;
        public List<OrderLine> Products { get; set; } = [];
       
        // public Truck(int truckId, double truckWeight)
        // {
        //     TruckID = truckId;s
        //     TruckWeight = truckWeight;            
        // }
        public Truck() { }
    }
}