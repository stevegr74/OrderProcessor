using Newtonsoft.Json;
using System;
using System.Configuration;
using System.Data.SqlClient;

namespace OrderProcessor
{
    public class Shipment
    {
        [JsonProperty(PropertyName = "order reference")]
        public string OrderReference { get; set; }
        [JsonProperty(PropertyName = "marketplace")]
        public string OrderMarketplace { get; set; }
        [JsonProperty(PropertyName = "postal service")]
        public string ShippingService { get; set; }
        [JsonProperty(PropertyName = "postcode")]
        public string Postcode { get; set; }

        public bool CommitToDatabase()
        {
            bool ret = false;
            using (SqlConnection sql = new SqlConnection(ConfigurationManager.ConnectionStrings["DB"].ConnectionString))
            {
                try
                {
                    //TODO: should parameterize this
                    SqlCommand sqlCommand = new SqlCommand("insert into tblShipment VALUES('" + OrderReference + "','" + OrderMarketplace + "','" + ShippingService + "','" + Postcode + "')", sql);
                    sqlCommand.Connection.Open();
                    sqlCommand.ExecuteNonQuery();
                    ret = true;
                }
                catch(Exception ex)
                {
                    Logger.Log("Exception caught: " + ex.Source + " : " + ex.Message);
                }
            }
            return ret;
        }
    }
}
