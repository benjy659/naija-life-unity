// Naija Streets (Unity) - the conductor's two minigames as pure maths
// (docs/specs/jobs-m4.md section 6.1 steps 2 and 5). The Unity side draws them.

using System;

namespace NaijaLife.Jobs
{
    /// <summary>The shout ring: shrinks from 2x to 0.4x each period; gold when within the band around 1x.</summary>
    public sealed class ShoutRound
    {
        public const double EarlyLockSeconds = 0.45;
        public const double HitLockSeconds = 0.22;

        readonly int _level;
        double _lockUntil;

        public int Seats { get; private set; }
        public double Elapsed { get; private set; }
        public bool Finished => Seats >= ShiftRules.Seats || Elapsed >= ShiftRules.LoadSeconds;
        public double SecondsLeft => Math.Max(0, ShiftRules.LoadSeconds - Elapsed);

        public ShoutRound(int level) { _level = Math.Max(1, level); }

        public double RingScale => 2.0 - (Elapsed % ShiftRules.RingPeriodSeconds(_level)) / ShiftRules.RingPeriodSeconds(_level) * 1.6;
        public bool IsGold => Math.Abs(RingScale - 1.0) < ShiftRules.GoldHalfWidth(_level);

        public void Tick(double dt) { if (!Finished && dt > 0) Elapsed += dt; }

        public enum Tap { Ignored, Hit, Early }

        /// <summary>A tap on the Shout button at the current time.</summary>
        public Tap Shout()
        {
            if (Finished || Elapsed < _lockUntil) return Tap.Ignored;
            if (IsGold) { Seats++; _lockUntil = Elapsed + HitLockSeconds; return Tap.Hit; }
            _lockUntil = Elapsed + EarlyLockSeconds;
            return Tap.Early;
        }

        /// <summary>Fewer than 4 passengers: the driver leaves without you.</summary>
        public bool Departs => Seats >= ShiftRules.MinSeatsToDepart;
    }

    /// <summary>One fare: a passenger pays with a big note; one of three change amounts is right.</summary>
    public struct ChangeQuestion
    {
        public int Note;          // 1000 or 2000
        public int Correct;       // Note - Fare
        public int[] Options;     // 3 amounts, shuffled, all positive, all different

        public static ChangeQuestion Make(Random rng)
        {
            int note = rng.Next(2) == 0 ? 1000 : 2000;
            int ok = note - ShiftRules.Fare;
            int[] wrongs = { ok - 200, ok + 300, ok - 100, ok + 200 };
            int w1 = wrongs[rng.Next(wrongs.Length)], w2;
            do { w2 = wrongs[rng.Next(wrongs.Length)]; } while (w2 == w1);
            int[] opts = { ok, w1, w2 };
            for (int i = opts.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); int t = opts[i]; opts[i] = opts[j]; opts[j] = t; }
            return new ChangeQuestion { Note = note, Correct = ok, Options = opts };
        }

        public string ResultText(int picked) =>
            picked == Correct ? "Correct."
            : picked > Correct ? "You don dash am ₦" + (picked - Correct) + "!"
            : "My change no complete!";
    }
}
