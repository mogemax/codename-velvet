using System;
using CodenameVelvet.DayNight;
using NUnit.Framework;

namespace CodenameVelvet.Tests
{
    public class DayNightClockTests
    {
        [Test]
        public void Starts_OnFirstDay_WithFullTime()
        {
            var clock = new DayNightClock(100f, 50f);
            Assert.That(clock.Phase, Is.EqualTo(DayPhase.Day));
            Assert.That(clock.NightNumber, Is.EqualTo(0));
            Assert.That(clock.Remaining, Is.EqualTo(100f));
            Assert.That(clock.PhaseProgress, Is.EqualTo(0f));
        }

        [Test]
        public void Advance_WithinPhase_DoesNotChange()
        {
            var clock = new DayNightClock(100f, 50f);
            Assert.That(clock.Advance(40f), Is.False);
            Assert.That(clock.Remaining, Is.EqualTo(60f));
            Assert.That(clock.PhaseProgress, Is.EqualTo(0.4f).Within(1e-5));
        }

        [Test]
        public void Advance_PastDay_EntersFirstNight_CarryingOverflow()
        {
            var clock = new DayNightClock(100f, 50f);
            Assert.That(clock.Advance(110f), Is.True);
            Assert.That(clock.Phase, Is.EqualTo(DayPhase.Night));
            Assert.That(clock.NightNumber, Is.EqualTo(1));
            Assert.That(clock.Remaining, Is.EqualTo(40f).Within(1e-4));
        }

        [Test]
        public void Advance_PastNight_ReturnsToDay_KeepingNightCount()
        {
            var clock = new DayNightClock(100f, 50f);
            clock.Advance(100f);
            clock.Advance(50f);
            Assert.That(clock.Phase, Is.EqualTo(DayPhase.Day));
            Assert.That(clock.NightNumber, Is.EqualTo(1));
            Assert.That(clock.Remaining, Is.EqualTo(100f));
        }

        [Test]
        public void Advance_HugeStep_CrossesSeveralPhases()
        {
            var clock = new DayNightClock(10f, 5f);
            clock.Advance(28f); // día 10 + noche 5 + día 10 + 3 de la segunda noche
            Assert.That(clock.Phase, Is.EqualTo(DayPhase.Night));
            Assert.That(clock.NightNumber, Is.EqualTo(2));
            Assert.That(clock.Remaining, Is.EqualTo(2f).Within(1e-4));
        }

        [Test]
        public void SkipPhase_JumpsToNextPhaseWithFullTime()
        {
            var clock = new DayNightClock(100f, 50f);
            clock.Advance(30f);
            clock.SkipPhase();
            Assert.That(clock.Phase, Is.EqualTo(DayPhase.Night));
            Assert.That(clock.NightNumber, Is.EqualTo(1));
            Assert.That(clock.Remaining, Is.EqualTo(50f));
        }

        [Test]
        public void InvalidArguments_Throw()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new DayNightClock(0f, 10f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DayNightClock(10f, -1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => new DayNightClock(10f, 10f).Advance(-1f));
        }
    }

    public class NightThreatTests
    {
        [Test]
        public void FirstNight_OnePlayer_IsBase()
        {
            Assert.That(NightThreat.Multiplier(1, 1, 0.25f, 0.35f, 3f), Is.EqualTo(1f));
        }

        [Test]
        public void GrowsWithNights()
        {
            Assert.That(NightThreat.Multiplier(3, 1, 0.25f, 0.35f, 3f), Is.EqualTo(1.5f).Within(1e-5));
        }

        [Test]
        public void GrowsWithPlayers()
        {
            Assert.That(NightThreat.Multiplier(1, 3, 0.25f, 0.35f, 3f), Is.EqualTo(1.7f).Within(1e-5));
        }

        [Test]
        public void NightsAndPlayersCombine()
        {
            Assert.That(NightThreat.Multiplier(2, 2, 0.25f, 0.35f, 3f), Is.EqualTo(1.25f * 1.35f).Within(1e-5));
        }

        [Test]
        public void IsCappedAtMax()
        {
            Assert.That(NightThreat.Multiplier(20, 4, 0.25f, 0.35f, 3f), Is.EqualTo(3f));
        }

        [Test]
        public void NeverBelowOne()
        {
            Assert.That(NightThreat.Multiplier(0, 0, -1f, -1f, 0.5f), Is.EqualTo(1f));
        }

        [Test]
        public void EnemyCount_RoundsAndClamps()
        {
            Assert.That(NightThreat.EnemyCount(6, 1f), Is.EqualTo(6));
            Assert.That(NightThreat.EnemyCount(6, 1.25f), Is.EqualTo(8)); // 7.5 → 8
            Assert.That(NightThreat.EnemyCount(6, 1.7f), Is.EqualTo(10)); // 10.2 → 10
            Assert.That(NightThreat.EnemyCount(-3, 2f), Is.EqualTo(0));
        }
    }
}
