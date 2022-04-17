using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ComputerResetApi.Models;
using System;

namespace ComputerResetApi.Services
{
    public interface IEventService
    {
        Task<OpenEvent> GetEventFrontPage(string facebookId);
        Task<OpenEvent> GetPrivateEventPage(string facebookId, Guid eventKey);
    }
    
    public class EventService : IEventService
    {
        private readonly Cr9525signupContext _context;

        public EventService(Cr9525signupContext context)
        {
            _context = context;
        }

        public async Task<OpenEvent> GetEventFrontPage(string facebookId) {
            var finalTimeslot = new List<TimeslotLimited>();
            var rtnTimeslot = new OpenEvent();
            DateTime limitTime = DateTime.UtcNow.AddHours(1);

            var openSlot = await(from t in _context.Timeslot
                    where DateTime.UtcNow >= t.EventOpenTms
                    && t.EventStartTms >= limitTime
                    && !t.PrivateEventInd
                    orderby t.EventStartTms
                    select new TimeslotLimitedDb {
                        Id = t.Id,
                        EventStartTms = t.EventStartTms,
                        EventEndTms = t.EventEndTms,
                        EventClosed = t.EventClosed,
                        EventNote = t.EventNote,
                        IntlEventInd = t.IntlEventInd,
                        EventSlotCnt = t.EventSlotCnt,
                        OverbookCnt = t.OverbookCnt
                    }).ToListAsync();

            //Since we can only have one signup per open weekend now, can use first or default here.
            // No longer!
            var userSignedupList = await (from es in _context.EventSignup
                    join u in _context.Users
                    on es.UserId equals u.Id
                    join t in _context.Timeslot
                    on es.TimeslotId equals t.Id
                    where u.FbId == facebookId
                    && DateTime.UtcNow >= t.EventOpenTms
                    && t.EventStartTms >= limitTime
                    && !t.PrivateEventInd
                    && !es.DeleteInd
                    select new {
                        es.Id,
                        es.TimeslotId,
                        es.AttendNbr,
                        es.ConfirmInd,
                        u.EventCnt,
                        es.FlexibleInd,
                        u.CountryCd
                    }).ToListAsync();

            // default value - removing functionality for move
            rtnTimeslot.MoveFlag = false;

            if (userSignedupList is null)
            {
                rtnTimeslot.SignedUpTimeslot = 0;
            }

            foreach (TimeslotLimitedDb eventSlot in openSlot) {
                var userSignSlot = userSignedupList.Find(u => u.TimeslotId == eventSlot.Id);

                if (userSignSlot != null) {
                    if (userSignSlot.AttendNbr <= eventSlot.EventSlotCnt && userSignSlot.ConfirmInd) {
                        eventSlot.UserSlot = "G";
                    } else if (userSignSlot.AttendNbr <= eventSlot.EventSlotCnt) {
                        eventSlot.UserSlot = "S";
                    } else if (userSignSlot.AttendNbr <= eventSlot.EventSlotCnt + eventSlot.OverbookCnt) {
                        eventSlot.UserSlot = "C";
                    } else if (userSignSlot.TimeslotId != null) {
                        eventSlot.UserSlot = "L";
                    }
                }

                var intlAttendee = userSignSlot?.CountryCd is not null;

                finalTimeslot.Add(new TimeslotLimited() {
                    Id = eventSlot.Id,
                    EventStartTms = eventSlot.EventStartTms,
                    EventEndTms = eventSlot.EventEndTms,
                    UserSlot = eventSlot.UserSlot,
                    EventClosed = eventSlot.EventClosed,
                    EventNote = eventSlot.EventNote,
                    IntlEventInd = eventSlot.IntlEventInd == false ? intlAttendee : eventSlot.IntlEventInd,                    
                });

            }

            //assign compiled list to return var
            rtnTimeslot.Timeslot = finalTimeslot;

            return rtnTimeslot;
        }
        
        // This is very quick and dirty and in an ideal world would be refactored
        // But since it's a month until the end...
        public async Task<OpenEvent> GetPrivateEventPage(string facebookId, Guid eventKey) {
            var finalTimeslot = new List<TimeslotLimited>();
            var rtnTimeslot = new OpenEvent();
            DateTime limitTime = DateTime.UtcNow.AddHours(1);

            var openSlot = await(from t in _context.Timeslot
                    where DateTime.UtcNow >= t.EventOpenTms
                    && t.EventStartTms >= limitTime
                    && t.PrivateEventInd
                    && t.EventKey == eventKey
                    orderby t.EventStartTms
                    select new TimeslotLimitedDb {
                        Id = t.Id,
                        EventStartTms = t.EventStartTms,
                        EventEndTms = t.EventEndTms,
                        EventClosed = t.EventClosed,
                        EventNote = t.EventNote,
                        IntlEventInd = t.IntlEventInd,
                        EventSlotCnt = t.EventSlotCnt,
                        OverbookCnt = t.OverbookCnt
                    }).ToListAsync();

            //Since we can only have one signup per open weekend now, can use first or default here.
            // No longer!
            var userSignedupList = await (from es in _context.EventSignup
                    join u in _context.Users
                    on es.UserId equals u.Id
                    join t in _context.Timeslot
                    on es.TimeslotId equals t.Id
                    where u.FbId == facebookId
                    && DateTime.UtcNow >= t.EventOpenTms
                    && t.EventStartTms >= limitTime
                    && t.PrivateEventInd
                    && t.EventKey == eventKey
                    && !es.DeleteInd
                    select new {
                        es.Id,
                        es.TimeslotId,
                        es.AttendNbr,
                        es.ConfirmInd,
                        u.EventCnt,
                        es.FlexibleInd,
                        u.CountryCd
                    }).ToListAsync();

            // default value - removing functionality for move
            rtnTimeslot.MoveFlag = false;

            if (userSignedupList is null)
            {
                rtnTimeslot.SignedUpTimeslot = 0;
            }

            foreach (TimeslotLimitedDb eventSlot in openSlot) {
                var userSignSlot = userSignedupList.Find(u => u.TimeslotId == eventSlot.Id);

                if (userSignSlot != null) {
                    if (userSignSlot.AttendNbr <= eventSlot.EventSlotCnt && userSignSlot.ConfirmInd) {
                        eventSlot.UserSlot = "G";
                    } else if (userSignSlot.AttendNbr <= eventSlot.EventSlotCnt) {
                        eventSlot.UserSlot = "S";
                    } else if (userSignSlot.AttendNbr <= eventSlot.EventSlotCnt + eventSlot.OverbookCnt) {
                        eventSlot.UserSlot = "C";
                    } else if (userSignSlot.TimeslotId != null) {
                        eventSlot.UserSlot = "L";
                    }
                }

                var intlAttendee = !(userSignSlot?.CountryCd is null);

                finalTimeslot.Add(new TimeslotLimited() {
                    Id = eventSlot.Id,
                    EventStartTms = eventSlot.EventStartTms,
                    EventEndTms = eventSlot.EventEndTms,
                    UserSlot = eventSlot.UserSlot,
                    EventClosed = eventSlot.EventClosed,
                    EventNote = eventSlot.EventNote,
                    IntlEventInd = eventSlot.IntlEventInd == false ? intlAttendee : eventSlot.IntlEventInd,                    
                });

            }

            //assign compiled list to return var
            rtnTimeslot.Timeslot = finalTimeslot;

            return rtnTimeslot;
        }
    }

}