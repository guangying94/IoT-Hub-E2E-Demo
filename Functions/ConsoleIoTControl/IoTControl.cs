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
using Newtonsoft.Json.Linq;

namespace ConsoleIoTControl
{
    public static class IoTControl
    {
        public static string connectionString = Environment.GetEnvironmentVariable("IOT_HUB_CONNECTION_STRING");

        [FunctionName("IoTControl")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string responseMessage = "";

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            HttpBody data = JsonConvert.DeserializeObject<HttpBody>(requestBody);
            string operation = data.operation;
            string deviceName = data.deviceName;

            try
            {
                ServiceClient serviceClient = ServiceClient.CreateFromConnectionString(connectionString);
                var methodInvocation = new CloudToDeviceMethod(operation);
                await serviceClient.InvokeDeviceMethodAsync(deviceName, methodInvocation);

                responseMessage = string.IsNullOrEmpty(operation)
    ? "This HTTP triggered function executed successfully. However, there's missing operation. Accepted operation: StartSimulator, StopSimulator, SetTurbo, StopTurbo."
    : $"This HTTP triggered function executed successfully. {operation} command is sent to {deviceName}.";
            }
            catch (Exception e)
            {
                log.LogInformation(e.Message);
                responseMessage = "Error. Please check if device name and operation name is correct.";
            }

            return new OkObjectResult(responseMessage);
        }

        public class HttpBody
        {
            public string operation { get; set; }
            public string deviceName { get; set; }
        }
    }
}
