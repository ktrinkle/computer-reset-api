using System;

namespace ComputerResetApi.Helpers
{
    public class AppSettings
    {
        public string Secret { get; set; }
        public string User { get; set; }
        public string Shaker { get; set; }
        public string[] Whitelist { get; set; }
        public string SignupOpen{ get; set; }
    }
}