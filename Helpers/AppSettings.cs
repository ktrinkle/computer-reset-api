using System;

namespace ComputerResetApi.Helpers
{
    public class AppSettings
    {
        public string Secret { get; set; }
        public string SignupOpen{ get; set; }
        public int? AutoClear { get; set; }
        public string FacebookAuthUrl { get; set; }
    }
}