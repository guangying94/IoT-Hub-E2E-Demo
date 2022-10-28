using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.Azure.Devices;
using System.Net.Http;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Azure.DigitalTwins.Core;
using Azure.Identity;
using Azure;

namespace ConsoleIoTControl
{
    public static class UnexpectedShutdown
    {
        public static string connectionString = Environment.GetEnvironmentVariable("IOT_HUB_CONNECTION_STRING");
        public static string fault_url = Environment.GetEnvironmentVariable("FAULT_AZURE_FUNCTION");
        public static string aml_url = Environment.GetEnvironmentVariable("AML_ENDPOINT");
        private static readonly string adtInstanceUrl = Environment.GetEnvironmentVariable("ADT_SERVICE_URL");

        [FunctionName("UnexpectedShutdown")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function [Unexpected Shutdown] processed a request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            string responseMessage;

            try
            {
                List<HttpBody> raw_data = JsonConvert.DeserializeObject<List<HttpBody>>(requestBody);
                HttpBody data = raw_data.FirstOrDefault();
                string deviceName = data.deviceName;
                
                // Query against AML endpoint
                HttpClient client = new HttpClient();
                AMLRequest amlRequest = new AMLRequest();
                GlobalParameters globalParameter = new GlobalParameters();
                globalParameter.method = "predict";
                amlRequest.GlobalParameters = globalParameter;

                Telemetry _telemetry = new Telemetry();
                _telemetry.temperature = Convert.ToDouble(data.temp);
                _telemetry.humidity = Convert.ToDouble(data.humid);
                _telemetry.revolution = Convert.ToInt32(data.rev);

                Inputs _input = new Inputs();
                List<Telemetry> _data = new List<Telemetry>();
                _data.Add(_telemetry);
                _input.data = _data;

                amlRequest.Inputs = _input;

                string check = JsonConvert.SerializeObject(amlRequest);
                var webResponse = await client.PostAsync(aml_url, new StringContent(check, Encoding.UTF8, "application/json"));
                var response = await webResponse.Content.ReadAsStringAsync();

                AMLResponse prediction = JsonConvert.DeserializeObject<AMLResponse>(response);
                data.prediction = prediction.Results.FirstOrDefault();

                if (prediction.Results.FirstOrDefault() >= 2)
                {
                    ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(connectionString);
                    var methodInvocation = new CloudToDeviceMethod("StopSimulator");
                    await serviceClient.InvokeDeviceMethodAsync(deviceName, methodInvocation);
                    
                    responseMessage = "Fault detected";
                }
                else
                {
                    responseMessage = "Anomaly detected, but not fault yet.";
                }

                //send message to Logic App
                webResponse = await client.PostAsync(fault_url, new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8,"application/json"));

                //send message to ADT
                var cred = new DefaultAzureCredential();
                var adt_client = new DigitalTwinsClient(new Uri(adtInstanceUrl), cred);
                var updateTwinData = new JsonPatchDocument();
                updateTwinData.AppendReplace("/Temperature", data.temp);
                updateTwinData.AppendReplace("/Humidity", data.humid);
                updateTwinData.AppendReplace("/Revolution", data.rev);
                updateTwinData.AppendReplace("/IsAnomaly", data.prediction);
                await adt_client.UpdateDigitalTwinAsync(deviceName, updateTwinData);
            }
            catch (Exception e)
            {
                responseMessage = e.Message;
            }

            return new OkObjectResult(responseMessage);
        }

        public class HttpBody
        {
            public double temp { get; set; }
            public double humid { get; set; }
            public int rev { get; set; }
            public string deviceName { get; set; }
            public int prediction { get; set; }
        }

        public class Telemetry
        {
            public double temperature { get; set; }
            public double humidity { get; set; }
            public int revolution { get; set; }
        }

        public class GlobalParameters
        {
            public string method { get; set; }
        }

        public class Inputs
        {
            public List<Telemetry> data { get; set; }
        }

        public class AMLRequest
        {
            public Inputs Inputs { get; set; }
            public GlobalParameters GlobalParameters { get; set; }
        }

        public class AMLResponse
        {
            public List<int> Results { get; set; }
        }
    }
}
