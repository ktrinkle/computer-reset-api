using System;
using System.Collections.Generic;

namespace ComputerResetApi
{
    public partial class EventSignup
    {
        public int? TimeslotId { get; set; }
        public int? UserId { get; set; }
        public DateTime? SignupTms { get; set; }
        public int? AttendNbr { get; set; }
        public bool? AttendInd { get; set; }
        public int Id { get; set; }
        public string SignupTxt { get; set; }
        public bool ConfirmInd { get; set; }
        public bool DeleteInd { get; set; }
        public bool NoShowInd { get; set; }
        public bool FlexibleInd { get; set; }

        public virtual Timeslot Timeslot { get; set; }
        public virtual Users User { get; set; }
    }

    public partial class EventSignupCall
    {
        public string FbId { get; set; }
        public int EventId { get; set; }
        public string CityNm { get; set; }
        public string StateCd { get; set; }
        public string CountryCd { get; set; }
        public string Realname { get; set; }
        public string FirstNm { get; set; }
        public string LastNm { get; set; }
        public bool FlexibleInd { get; set; }
    }

    public partial class EventSignupNote
    {
        public int Id { get; set; }
        public string SignupTxt { get; set; }
        public string FbId { get; set; }
    }
}
