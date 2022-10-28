    public class DeviceTwinObject
    {
        public object deviceId { get; set; }
        public object etag { get; set; }
        public object version { get; set; }
        public Properties properties { get; set; }
    }

    public class Properties
    {
        public Desired desired { get; set; }
        public Reported reported { get; set; }
    }

    public class Desired
    {
        public Telemetry telemetry { get; set; }
        public int version { get; set; }
    }

    public class Telemetry
    {
        public int frequency { get; set; }
    }

    public class Reported
    {
        public Telemetry1 telemetry { get; set; }
        public int version { get; set; }
    }

    public class Telemetry1
    {
        public int frequency { get; set; }
    }