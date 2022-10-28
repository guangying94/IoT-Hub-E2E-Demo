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
using System.Globalization;

namespace ConsoleIoTControl
{
    public static class UpdateFrequency
    {
        public static string connectionString = Environment.GetEnvironmentVariable("IOT_HUB_CONNECTION_STRING");

        [FunctionName("UpdateFrequency")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function [Update Frequency] processed a request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            HttpBody data = JsonConvert.DeserializeObject<HttpBody>(requestBody);
            string frequencyString = data.frequency;
            string deviceName = data.deviceName;

            try
            {
                RegistryManager registryManager;
                registryManager = RegistryManager.CreateFromConnectionString(connectionString);
                AddTagsAndQuery(registryManager, Convert.ToInt32(frequencyString), deviceName).Wait();

                ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(connectionString);
                var methodInvocation = new CloudToDeviceMethod("UpdateProperties");
                await Task.Delay(100);
                await serviceClient.InvokeDeviceMethodAsync(deviceName, methodInvocation);

                return frequencyString != null && deviceName != null
                    ? (ActionResult)new OkObjectResult($"Updated desired properties to {frequencyString} ms.")
                    : new BadRequestObjectResult("Error. Do you have missing field?");
            }
            catch (Exception e)
            {
                log.LogError(e.Message);

                return new BadRequestObjectResult("Error. Please check your body request.");
            }
        }

        public static async Task AddTagsAndQuery(RegistryManager registryManager, int updateFreq, string deviceName)
        {
            var twin = await registryManager.GetTwinAsync(deviceName);
            string prop = $"\"frequency\":{updateFreq}";
            var patch = "{\"properties\":{\"desired\":{\"telemetry\":{" + prop + "}}}}";

            await registryManager.UpdateTwinAsync(twin.DeviceId, patch, twin.ETag);
        }

        public class HttpBody
        {
            public string frequency { get; set; }
            public string deviceName { get; set; }
        }
    }
}
