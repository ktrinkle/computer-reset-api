using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace ComputerResetApi
{
    public partial class Timeslot
    {
        public int Id { get; set; }
        public DateTime EventStartTms { get; set; }
        public DateTime EventEndTms { get; set; }
        public int EventSlotCnt { get; set; }
        public DateTime EventOpenTms { get; set; }
        public bool? EventClosed { get; set; }
        public int? OverbookCnt { get; set; }
        public int? SignupCnt { get; set; }
        public string EventNote { get; set; }
        public bool PrivateEventInd { get; set; }
        public bool? IntlEventInd { get; set; }
    }

    public partial class TimeslotLimited
    {
        [Column("id")]
        public int Id { get; set; }
        [Column("eventstarttms")]
        public DateTime EventStartTms { get; set; }
        [Column("eventendtms")]
        public DateTime EventEndTms { get; set; }
        [Column("userslot")]
        public string UserSlot { get; set; }
        [Column("eventclosed")]
        public bool? EventClosed { get; set; }
        [Column("eventnote")]
        public string EventNote { get; set; }
        [Column("intleventind")]
        public bool? IntlEventInd { get; set; }

    }

     public partial class TimeslotLimitedDb
    {
        public int Id { get; set; }
        public DateTime EventStartTms { get; set; }
        public DateTime EventEndTms { get; set; }
        public string UserSlot { get; set; }
        public bool? EventClosed { get; set; }
        public string EventNote { get; set; }
        public bool? IntlEventInd { get; set; }
        public int EventSlotCnt {get; set; }
        public int? OverbookCnt { get; set; }

    }

    public partial class OpenEvent
    {
        public List<TimeslotLimited> Timeslot { get; set; }
        public int? SignedUpTimeslot { get; set; }
        public bool MoveFlag { get; set; }
    } 

    public partial class TimeslotAdmin {
        public int Id { get; set; }
        public DateTime EventStartTms { get; set; }
        public DateTime EventEndTms { get; set; }
        public int EventSlotCnt { get; set; }
        public DateTime EventOpenTms { get; set; }
        public bool? EventClosed { get; set; }
        public int? OverbookCnt { get; set; }
        public int? SignupCnt { get; set; }
        public string facebookId { get; set; }
        public string EventNote { get; set; }
        public bool PrivateEventInd { get; set; }
        public bool? IntlEventInd { get; set; }
    }

    public partial class TimeslotStandby
    {
        public int Id { get; set; }
        public DateTime EventDate { get; set; }
        public int EventSlotCnt { get; set; }
        public int AvailSlot { get; set; }
    }

}
