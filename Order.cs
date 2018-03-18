using Newtonsoft.Json;
using System;
using System.Configuration;
using System.Data.SqlClient;

namespace OrderProcessor
{
    public class Order
    {
        [JsonProperty(PropertyName = "order reference")]
        public string OrderReference { get; set; }
        [JsonProperty(PropertyName = "marketplace")]
        public string OrderMarketplace { get; set; }
        [JsonProperty(PropertyName = "name")]
        public string OrderFirstname { get; set; }
        [JsonProperty(PropertyName = "surname")]
        public string OrderSurname { get; set; }

        public bool CommitToDatabase()
        {
            bool ret = false;
            using (SqlConnection sql = new SqlConnection(ConfigurationManager.ConnectionStrings["DB"].ConnectionString))
            {
                try
                {
                    //TODO: should parameterize this
                    SqlCommand sqlCommand = new SqlCommand("insert into tblOrder VALUES('" + OrderReference + "','" + OrderMarketplace + "','" + OrderFirstname + "','" + OrderSurname + "',0)", sql);
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
