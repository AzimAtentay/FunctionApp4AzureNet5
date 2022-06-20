using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Newtonsoft.Json;

namespace FunctionApp4AzureNet5
{
    public class Function1
    {
        private readonly ILogger _logger;
        private string m_connString = "AuthType='ClientSecret'; ServiceUri='https://azim.crm4.dynamics.com'; ClientId = 'fe9a7773-3a3f-4cac-8ae1-496ac5ae54f1'; ClientSecret = 'G.S8Q~T-x3VrvkLFMn~Txi6Peu.1nkdrSpseVbXy';";

        public Function1(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<Function1>();
        }

        public DateTime StartOn { get; set; }

        public DateTime EndOn { get; set; }


        [Function("AzAtFunction")]
        public HttpResponseData Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            string requestBody =  new StreamReader(req.Body).ReadToEnd();
            dynamic data = JsonConvert.DeserializeObject(requestBody);

            // ***  Checking input values ****
            string startOn_s = data?.StartOn;
            string endOn_s = data?.EndOn;
            if (string.IsNullOrEmpty(startOn_s) || string.IsNullOrEmpty(endOn_s))
            {
                response.WriteString("StartOn or EndOn empty");
                return response;
            }
            
            DateTime dt_startOn;
            DateTime dt_endOn;
            if ((DateTime.TryParse(startOn_s, out dt_startOn)) && (DateTime.TryParse(endOn_s, out dt_endOn)))
            {
                this.StartOn = dt_startOn.Date;
                this.EndOn = dt_endOn.Date;
            }
            else
            {
                response.WriteString("StartOn or EndOn has wrong format");
                return response;
            }
            

            // *** Connecting and processing ***
            ServiceClient service = null;
            var result = string.Empty;
            try
            {
                service = SetConnection();
                result = MakeNew_msdyn_timeentry(service);
            }
            catch(Exception e)
            {
                result = "Error. Message: " + e.Message;
            }
            finally
            {
                if  (service!=null) service.Dispose();
            }

            response.WriteString("Status: " + result);                        
            return response;
        }

        /// <summary>
        /// Connecting and start processing
        /// </summary>
        /// <param name="connString">Optional parameter</param>
        /// <returns></returns>
        public ServiceClient SetConnection(string connString = null)
        {
            //string connectionString = "AuthType='ClientSecret'; ServiceUri='https://azim.crm4.dynamics.com'; ClientId = 'fe9a7773-XXXX-4cac-XXXX-496ac5aeXXXX'; ClientSecret = 'G.XXXX-x3VrvkLFMn~XXXXPeu.1nkdrSpseVbXy'; ";

            // Get Connection strings(put local and Azure env together)
            var connParameter = "ConnString4Dataverse";
            var connectionString = System.Environment.GetEnvironmentVariable(connParameter);
            // здесь используется m_connString, потому что команда " System.Environment.GetEnvironmentVariable(connParameter);" возвращает null при отработке xUnit.
            ServiceClient service = new ServiceClient(connString??connectionString?? m_connString);

            //WhoAmIResponse whoAmIResponse = (WhoAmIResponse)service.Execute(new WhoAmIRequest());
            //string result = ($"Connected with UserId: {whoAmIResponse.UserId}");

            return service;
        }
        
        /// <summary>
        /// Main process for store dates
        /// </summary>
        /// <param name="service"></param>
        /// <returns></returns>
        public string MakeNew_msdyn_timeentry(ServiceClient service)
        {
            // request for custom interval ==
            // if no records then save new 
            // else show message "interval itersects existing data".
            int countOfExist = RetrieveMultiple(service, this.StartOn, this.EndOn);
            
            if (countOfExist < 0)
            {
                return $"Error when RetrieveMultiple days";
            }

            if (countOfExist>0)
            {
                return $"The interval intersects existing data ({countOfExist} days)";
            }

            return StoreIntervalOfDays(service, this.StartOn, this.EndOn);
        }

        public string StoreIntervalOfDays(ServiceClient service, DateTime StartOn, DateTime EndOn)
        {
            for (var storeDay = StartOn.Date; storeDay.Date <= EndOn.Date; storeDay = storeDay.AddDays(1))
            {
                var res = CreateNewRecord(service, storeDay);
                if (!string.IsNullOrEmpty(res))
                {
                    return $"Error when saving day: {storeDay},  Message: {res}";
                }
            }

            return $"Interval saved successfully";
        }


        /// <summary>
        /// Store new one Day to Dataverse
        /// </summary>
        /// <param name="service"></param>
        /// <param name="storeDay"></param>
        /// <param name="description">not mandatory</param>
        /// <returns></returns>
        public string CreateNewRecord(ServiceClient service, DateTime storeDay, string description = "")
        {
            Entity createAccount = new Entity("msdyn_timeentry");           
            createAccount["msdyn_start"] = storeDay.Date.AddHours(12); // set to noon
            createAccount["msdyn_end"] = storeDay.Date.AddHours(12).AddMinutes(5);// set to 5 minutes after noon
            createAccount["msdyn_duration"] = 0; // mandatory            
            createAccount["msdyn_description"] = (description!="")?description:$"Record for RentReady. Date = {storeDay.ToString("d")}, inerval: {this.StartOn.ToString("d")} - {this.EndOn.ToString("d")}";

            System.Guid createAccountId;
            // Now create that account in Dataverse. Note that the Dataverse
            try
            {
                createAccountId = service.Create(createAccount);                
            }
            catch (Exception e)
            {
                return $"Error on Create: {e.Message}";
            }

            return "";
        }

        /// <summary>
        /// Request for existing dates in interval
        /// </summary>
        /// <param name="service"></param>
        /// <param name="startDate"></param>
        /// <param name="endDate"></param>
        /// <returns></returns>
        public int RetrieveMultiple(ServiceClient service, DateTime startDate, DateTime endDate)
        {
            // *** Request Active records ****
            ConditionExpression condition1 = new ConditionExpression();
            condition1.AttributeName = "statecode";
            condition1.Operator = ConditionOperator.Equal;
            condition1.Values.Add(0);

            FilterExpression filter1 = new FilterExpression();
            filter1.Conditions.Add(condition1);

            // *** add startDate to filter ****
            ConditionExpression condition2 = new ConditionExpression("msdyn_start", ConditionOperator.GreaterEqual, startDate.Date.AddMinutes(1));

            filter1.FilterOperator = LogicalOperator.And;
            filter1.Conditions.Add(condition2);

            // *** add endDate to filter ****
            ConditionExpression condition3 = new ConditionExpression("msdyn_end", ConditionOperator.LessEqual, endDate.Date.AddDays(1).AddMinutes(-1));

            filter1.FilterOperator = LogicalOperator.And;
            filter1.Conditions.Add(condition3);

            QueryExpression query = new QueryExpression("msdyn_timeentry");
            query.ColumnSet.AddColumns("msdyn_start", "msdyn_end", "msdyn_duration", "ownerid", "msdyn_description");
            query.Criteria = filter1;

            try
            {
                EntityCollection result1 = service.RetrieveMultiple(query);
                return result1.Entities.Count;
            }
            catch (Exception e)
            {
                _logger.LogInformation($"Error in module RetrieveMultiple: {e.Message}");
                return -1;
            }
        }

        //string GetEntityCollectionAsText(EntityCollection result1) {
        //    var res = string.Empty;
        //    foreach (var a in result1.Entities)
        //    {
        //        res += string.Format(", msdyn_start: " + a.Attributes["msdyn_start"] + ", msdyn_duration = " + a.Attributes["msdyn_duration"] +
        //            ", msdyn_end = " + a.Attributes["msdyn_end"]);
        //        if (a.Attributes.Keys.Contains("msdyn_description"))
        //        {
        //            res += ",  msdyn_description=" + a.Attributes["msdyn_description"];
        //        }
        //    }
        //    return res == string.Empty ? "!!! no data from msdyn_timeentry !!!" : " !!! Count of records: " + result1.Entities.Count;//newAccount["name"].ToString();
        //}

        /// <summary>
        /// Retrieve the existing account and its attributes.
        /// </summary>
        //string RetrieveOneExisting(ServiceClient service)
        //{
        //    Entity existAccount = new("msdyn_timeentry");
        //    ColumnSet attributes = new("msdyn_start", "msdyn_end", "ownerid", "statecode", "msdyn_description", "msdyn_duration", "msdyn_type");
        //    Guid accountid = new Guid("21557b3e-cee8-ec11-bb3c-000d3a29059c");
        //    existAccount = service.Retrieve(existAccount.LogicalName, accountid, attributes);

        //    return string.Format(", msdyn_start: " + existAccount.Attributes["msdyn_start"] + ", msdyn_duration = " + existAccount.Attributes["msdyn_duration"] +
        //            ", msdyn_end = " + existAccount.Attributes["msdyn_end"]);
        //}
    }
}
