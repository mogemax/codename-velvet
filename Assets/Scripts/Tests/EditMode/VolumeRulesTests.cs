using CodenameVelvet.Audio;
using NUnit.Framework;

namespace CodenameVelvet.Tests
{
    public class VolumeRulesTests
    {
        [Test]
        public void ToGain_MapsTheSliderRangeToAudioGain()
        {
            Assert.That(VolumeRules.ToGain(0f), Is.EqualTo(0f));
            Assert.That(VolumeRules.ToGain(50f), Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(VolumeRules.ToGain(100f), Is.EqualTo(1f));
        }

        [Test]
        public void OutOfRange_IsClamped()
        {
            Assert.That(VolumeRules.ToGain(-20f), Is.EqualTo(0f));
            Assert.That(VolumeRules.ToGain(250f), Is.EqualTo(1f));
            Assert.That(VolumeRules.DefaultPercent, Is.EqualTo(VolumeRules.MaxPercent));
        }
    }
}
