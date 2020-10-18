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

        public virtual Timeslot Timeslot { get; set; }
        public virtual Users User { get; set; }
    }

    public partial class EventSignupCall
    {
        public string fbId { get; set; }
        public int eventId { get; set; }
        public string cityNm { get; set; }
        public string stateCd { get; set; }
        public string realname { get; set; }
        public string firstNm { get; set; }
        public string lastNm { get; set; }
    }

    public partial class EventSignupNote
    {
        public int Id { get; set; }
        public string SignupTxt { get; set; }
        public string fbId { get; set; }
    }
}
