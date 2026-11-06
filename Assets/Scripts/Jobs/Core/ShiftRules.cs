// Naija Streets (Unity) - M4 shift rules. One place for every number in
// docs/specs/jobs-m4.md sections 6 and 7. The server (migration SQL, M4.4)
// and tools/jobsim.js use the same numbers; change all three together.
// Pure C#, no UnityEngine, so Edit Mode tests run it directly.

using System;

namespace NaijaLife.Jobs
{
    public static class ShiftRules
    {
        // Conductor trip
        public const int Seats = 14;
        public const int MinSeatsToDepart = 4;
        public const int FaresPerTrip = 4;
        public const int Fare = 500;
        public const int SeatPay = 60;
        public const int FullBusBonus = 200;
        public const int WrongChangeCost = 100;
        public const float LoadSeconds = 45f;
        public const float DriverLeftCooldownSeconds = 30f;
        public const float AnswerSeconds = 6f;

        // Shift limits (server clock)
        public const int ShiftMinutes = 15;
        public const int ShiftHardStopMinutes = 20;  // no trip accepted after this (a trip started by 15 min arrives by about 19)
        public const int StaleShiftMinutes = 20;     // lazy close
        public const int MinPaidMinutes = 10;
        public const int MinPaidTrips = 3;
        public const int MaxTripsPerShift = 6;
        public const int MinSecondsBetweenTrips = 150;
        public const int PaidShiftsPerDay = 3;

        // Pawa
        public const int PawaToClockIn = 25;
        public const float PawaDrainPerMinute = 1.2f;

        // Skill
        public const int MaxLevel = 10;
        public const int TripsPerLevelSquare = 25;

        /// <summary>1 + floor(sqrt(trips / 25)), capped at 10. Trips = counted lifetime trips (seats >= 4).</summary>
        public static int Level(int lifetimeTrips)
        {
            if (lifetimeTrips < 0) lifetimeTrips = 0;
            int l = 1 + (int)Math.Floor(Math.Sqrt(lifetimeTrips / (double)TripsPerLevelSquare));
            return Math.Min(MaxLevel, l);
        }

        /// <summary>0.65 at level 1 up to 1.415 at level 10.</summary>
        public static double Multiplier(int level)
        {
            level = Math.Max(1, Math.Min(MaxLevel, level));
            return 0.65 + 0.085 * (level - 1);
        }

        /// <summary>Server trip pay. 0 when fewer than 4 seats. Rounded to the nearest 10 naira.</summary>
        public static long TripPay(int seats, int faresRight, int level)
        {
            if (seats < MinSeatsToDepart) return 0;
            seats = Math.Min(seats, Seats);
            faresRight = Math.Max(0, Math.Min(FaresPerTrip, faresRight));
            int bas = SeatPay * seats + (seats >= Seats ? FullBusBonus : 0) - WrongChangeCost * (FaresPerTrip - faresRight);
            double pay = Math.Max(0, bas) * Multiplier(level);
            return (long)(Math.Round(pay / 10.0, MidpointRounding.AwayFromZero) * 10);
        }

        /// <summary>Most a shift can ever pay: 6 perfect trips at this level.</summary>
        public static long ShiftCeiling(int level) => MaxTripsPerShift * TripPay(Seats, FaresPerTrip, level);

        /// <summary>WAT day (Africa/Lagos is UTC+1 all year, no daylight saving).</summary>
        public static DateTime WatDay(DateTime utc) => utc.ToUniversalTime().AddHours(1).Date;

        /// <summary>A shift pays only with 10+ minutes and 3+ trips.</summary>
        public static bool IsPayable(DateTime startedUtc, DateTime endedUtc, int trips) =>
            (endedUtc - startedUtc).TotalMinutes >= MinPaidMinutes && trips >= MinPaidTrips;

        // Shout ring (spec 6.1 step 2)
        public static double GoldHalfWidth(int level) => 0.16 + 0.012 * Math.Max(1, Math.Min(MaxLevel, level));
        public static double RingPeriodSeconds(int level) => 1.35 - 0.015 * Math.Max(1, Math.Min(MaxLevel, level));
    }
}
