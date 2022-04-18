using System;

namespace ComputerResetApi.Helpers
{
    public class AppSettings
    {
        public string Secret { get; set; }
        public string SignupOpen{ get; set; }
        public int? AutoClear { get; set; }
        public string FacebookAuthUrl { get; set; }
        public string DevUserId { get; set; }
        public int? DumpsterCount { get; set; }
        public int? DumpsterVolume { get; set; }
        public int? AutoClearMinEvent { get; set; }
    }
}