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
    }
    
    public class EventService : IEventService
    {
        private readonly cr9525signupContext _context;

        public EventService(cr9525signupContext context)
        {
            _context = context;
        }

        public async Task<OpenEvent> GetEventFrontPage(string facebookId) {
            List<TimeslotLimited> finalTimeslot = new List<TimeslotLimited>();
            OpenEvent rtnTimeslot = new OpenEvent();
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
            var userSignSlot = await (from es in _context.EventSignup
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
                        es.FlexibleInd
                    }).SingleOrDefaultAsync();

            foreach (TimeslotLimitedDb eventSlot in openSlot) {
                if (userSignSlot != null && userSignSlot.TimeslotId == eventSlot.Id) {
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

                finalTimeslot.Add(new TimeslotLimited() {
                    Id = eventSlot.Id,
                    EventStartTms = eventSlot.EventStartTms,
                    EventEndTms = eventSlot.EventEndTms,
                    UserSlot = eventSlot.UserSlot,
                    EventClosed = eventSlot.EventClosed,
                    EventNote = eventSlot.EventNote,
                    IntlEventInd = eventSlot.IntlEventInd,                    
                });

            }

            //assign compiled list to return var
            rtnTimeslot.Timeslot = finalTimeslot;

            if (userSignSlot != null && userSignSlot.EventCnt < 2 && userSignSlot.AttendNbr == null) {
                //signed up, no slot, and < 2 visits, user should be able to move
                rtnTimeslot.SignedUpTimeslot = userSignSlot.Id;
                rtnTimeslot.MoveFlag = true;
                rtnTimeslot.FlexSlot = userSignSlot.FlexibleInd;
            } else if (userSignSlot == null) {
                //not signed up, we want to show the signup link
                rtnTimeslot.SignedUpTimeslot = 0;
                rtnTimeslot.MoveFlag = false;
                rtnTimeslot.FlexSlot = false;
            } else {
                //slot is picked and user > 1 visit. Flex ind only for user.
                rtnTimeslot.SignedUpTimeslot = -1;
                rtnTimeslot.MoveFlag = false;
                rtnTimeslot.FlexSlot = userSignSlot.FlexibleInd;
            }

            return rtnTimeslot;
        }
    }
}