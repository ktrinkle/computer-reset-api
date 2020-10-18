using System;
using System.Collections.Generic;

namespace ComputerResetApi
{
    public partial class Users
    {
        public int Id { get; set; }
        public string FirstNm { get; set; }
        public string LastNm { get; set; }
        public string CityNm { get; set; }
        public string StateCd { get; set; }
        public string RealNm { get; set; }
        public string FbId { get; set; }
        public bool? BanFlag { get; set; }
        public bool? AdminFlag { get; set; }
        public bool? VolunteerFlag { get; set; }
        public int? EventCnt { get; set; }

        public int? NoShowCnt { get; set; }
    }

    public partial class UserSmall
    {
        public string firstName { get; set; }
        public string lastName { get; set; }
        public string facebookId { get; set; }
    }

    public partial class UserAttrib
    {
        public string CityNm { get; set; }
        public string StateCd { get; set; }
        public string RealNm { get; set; }
        public bool? AdminFlag { get; set; }
        public bool? VolunteerFlag { get; set; }
     }
}
