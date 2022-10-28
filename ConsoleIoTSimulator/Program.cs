using System;
using System.Text;
using Newtonsoft.Json;
using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Shared;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

DeviceClient deviceClient;
string? iotHubName = Environment.GetEnvironmentVariable("IOT_HUB_NAME");
string? deviceId = Environment.GetEnvironmentVariable("DEVICE_ID");
string? sharedAccessKey = Environment.GetEnvironmentVariable("SHARED_ACCESS_KEY");
int telemetryFrequency = 1000;
bool sendData = true;
bool turboMode = false;


if (iotHubName != null && deviceId != null && sharedAccessKey != null)
{
    string iotHubConnectionString = $"HostName={iotHubName}.azure-devices.net;DeviceId={deviceId};SharedAccessKey={sharedAccessKey}";
    deviceClient = DeviceClient.CreateFromConnectionString(iotHubConnectionString, TransportType.Mqtt);
    InitClient();
    GetDeviceTwinProperty();
}
else
{
    Console.WriteLine("❌ Missing environment variable IOT_HUB_NAME, DEVICE_ID, SHARED_ACCESS_KEY");
}


void InitClient()
{
    try
    {
        Console.WriteLine("Connecting to Azure IoT Hub...");

        _ = deviceClient.OpenAsync();
        deviceClient.SetMethodHandlerAsync(nameof(StartSimulator), StartSimulator, null).Wait();
        deviceClient.SetMethodHandlerAsync(nameof(StopSimulator), StopSimulator, null).Wait();
        deviceClient.SetMethodHandlerAsync(nameof(ReportProperties), ReportProperties, null).Wait();
        deviceClient.SetMethodHandlerAsync(nameof(UpdateProperties), UpdateProperties, null).Wait();
        deviceClient.SetMethodHandlerAsync(nameof(SetTurbo), SetTurbo, null).Wait();
        deviceClient.SetMethodHandlerAsync(nameof(StopTurbo), StopTurbo, null).Wait();
        Console.WriteLine("✅ Device is connected to Azure IoT Hub");
        Console.ReadLine();
    }
    catch (Exception e)
    {
        Console.Write(e.Message);
    }

}

async void GetDeviceTwinProperty()
{
    try
    {
        Twin twin = await deviceClient.GetTwinAsync();
        DeviceTwinObject json = JsonConvert.DeserializeObject<DeviceTwinObject>(twin.ToJson());
        telemetryFrequency = json.properties.desired.telemetry.frequency;
        Console.WriteLine($"Current desired frequency is {telemetryFrequency} ms");
    }
    catch (Exception ex)
    {
        Console.WriteLine();
        Console.WriteLine(ex.Message);
    }
}

async void ReportFrequency(int frequency)
{
    try
    {
        Console.WriteLine("Sending frequency data as reported property");
        TwinCollection reportedProperties, telemetry;
        reportedProperties = new TwinCollection();
        telemetry = new TwinCollection();
        telemetry["frequency"] = frequency;
        reportedProperties["telemetry"] = telemetry;
        await deviceClient.UpdateReportedPropertiesAsync(reportedProperties);
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
    }
}


Task<MethodResponse> StartSimulator(MethodRequest methodRequest, object userContext)
{
    Console.WriteLine($"\n[Azure IoT Hub Direct Method] {nameof(StartSimulator)} is called.\n");
    SendDeviceToCloudMessageAsync();
    return Task.FromResult(new MethodResponse(new byte[0], 200));
}

Task<MethodResponse> ReportProperties(MethodRequest methodRequest, object userContext)
{
    Console.WriteLine($"\n[Azure IoT Hub Direct Method] {nameof(ReportProperties)} is called.\n");
    ReportFrequency(telemetryFrequency);
    return Task.FromResult(new MethodResponse(new byte[0], 200));
}

Task<MethodResponse> StopSimulator(MethodRequest methodRequest, object userContext)
{
    Console.WriteLine($"\n[Azure IoT Hub Direct Method] {nameof(StopSimulator)} is called.\n");
    sendData = false;
    Task.Delay(50);
    return Task.FromResult(new MethodResponse(new byte[0], 200));
}

Task<MethodResponse> UpdateProperties(MethodRequest methodQuest, object userContext)
{
    Console.WriteLine($"\n[Azure IoT Hub Direct Method] {nameof(UpdateProperties)} is called.\n");
    GetDeviceTwinProperty();
    return Task.FromResult(new MethodResponse(new byte[0], 200));
}

Task<MethodResponse> SetTurbo(MethodRequest methodRequest, object userContext)
{
    Console.WriteLine($"\n[Azure IoT Hub Direct Method] {nameof(SetTurbo)} is called.\n");
    turboMode = true;
    return Task.FromResult(new MethodResponse(new byte[0], 200));
}

Task<MethodResponse> StopTurbo(MethodRequest methodRequest, object userContext)
{
    Console.WriteLine($"\n[Azure IoT Hub Direct Method] {nameof(StopTurbo)} is called.\n");
    turboMode = false;
    return Task.FromResult(new MethodResponse(new byte[0], 200));
}


async void SendDeviceToCloudMessageAsync()
{
    double minTemperature = 25;
    double minHumidity = 63;
    int averageRevolution = 100;
    int messageId = 1;
    Random rand = new Random(DateTime.Now.Second);
    sendData = true;

    while (sendData)
    {
        double currentTemperature = minTemperature + rand.NextDouble() * 15;
        double currentHumidity = minHumidity + rand.NextDouble() * 20;
        int currentRevolution = averageRevolution;

        if (turboMode && rand.Next(12) == 3)
        {
            currentRevolution = averageRevolution + rand.Next(15, 30);
        }
        else
        {
            currentRevolution = averageRevolution - rand.Next(-3, 3);
        }
        string dataBuffer = $"{{\"messageId\":{messageId},\"temperature\":{currentTemperature},\"humidity\":{currentHumidity},\"revolution\":{currentRevolution},\"timestamp\":\"{(DateTime.UtcNow):O}\"}}";
        var message = new Message(Encoding.ASCII.GetBytes(dataBuffer));

        await deviceClient.SendEventAsync(message);
        Console.WriteLine("{0} > Sending message: {1}", DateTime.Now, dataBuffer);
        messageId++;
        await Task.Delay(telemetryFrequency);
    }
}
