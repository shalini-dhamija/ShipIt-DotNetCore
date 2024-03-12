namespace ShipIt.Models.ApiModels
{
    public class StockWeight
    {
        public int ProductId { get; set; }
        public string Gtin { get; set; }
        public int Quantity { get; set; }
        public float WeightInKg { get; set; }

        public StockWeight(int productId, string gtin, int quantity, float weight)
        {
            this.ProductId = productId;
            this.Gtin = gtin;
            this.Quantity = quantity;
            this.WeightInKg = weight;
        }
    }
}