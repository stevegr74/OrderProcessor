using Newtonsoft.Json;
using System;
using System.Configuration;
using System.Data.SqlClient;

namespace OrderProcessor
{
    public class OrderItem
    {
        [JsonProperty(PropertyName = "order reference")]
        public string OrderReference { get; set; }
        [JsonProperty(PropertyName = "marketplace")]
        public string OrderMarketplace { get; set; }
        [JsonProperty(PropertyName = "order item number")]
        public string OrderItemNumber { get; set; }
        [JsonProperty(PropertyName = "sku")]
        public string SKU { get; set; }
        [JsonProperty(PropertyName = "price per unit")]
        public double PricePerUnit { get; set; }
        [JsonProperty(PropertyName = "quantity")]
        public int Quantity { get; set; }

        public bool CommitToDatabase()
        {
            bool ret = false;
            using (SqlConnection sql = new SqlConnection(ConfigurationManager.ConnectionStrings["DB"].ConnectionString))
            {
                try
                {
                    //TODO: should parameterize this
                    SqlCommand sqlCommand = new SqlCommand("insert into tblOrderItem VALUES('" + OrderReference + "','" + OrderMarketplace + "','" + OrderItemNumber + "','" + SKU + "'," + PricePerUnit + "," + Quantity + ")", sql);
                    sqlCommand.Connection.Open();
                    sqlCommand.ExecuteNonQuery();
                    ret = true;
                }
                catch (Exception ex)
                {
                    Logger.Log("Exception caught: " + ex.Source + " : " + ex.Message);
                }
            }
            return ret;
        }
    }
}
