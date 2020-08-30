using System;
using System.Collections.Generic;

namespace ComputerResetApi
{
    public partial class Timeslot
    {
        public int Id { get; set; }
        public DateTime? EventStartTms { get; set; }
        public DateTime? EventEndTms { get; set; }
        public int? EventSlotCnt { get; set; }
        public DateTime? EventOpenTms { get; set; }
        public bool? EventClosed { get; set; }
        public int? OverbookCnt { get; set; }
        public int? SignupCnt { get; set; }
    }

     public partial class TimeslotLimited
    {
        public int Id { get; set; }
        public DateTime? EventStartTms { get; set; }
        public DateTime? EventEndTms { get; set; }
    }

    public partial class TimeslotAdmin
    {
        public int Id { get; set; }
        public DateTime? EventStartTms { get; set; }
        public DateTime? EventEndTms { get; set; }
        public int? EventSlotCnt { get; set; }
        public DateTime? EventOpenTms { get; set; }
        public bool? EventClosed { get; set; }
        public int? OverbookCnt { get; set; }
        public int? SignupCnt { get; set; }
        public string facebookId { get; set; }
    }
}
