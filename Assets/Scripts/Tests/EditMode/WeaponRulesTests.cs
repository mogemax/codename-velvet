using CodenameVelvet.Combat;
using NUnit.Framework;
using UnityEngine;

namespace CodenameVelvet.Tests
{
    public class WeaponRulesTests
    {
        private const float Tolerance = 1e-3f;

        [Test]
        public void SwingDamage_TheSwordsWithOne_BareHandedWithout()
        {
            Assert.That(WeaponRules.SwingDamage(false, 1, 2), Is.EqualTo(1));
            Assert.That(WeaponRules.SwingDamage(true, 1, 2), Is.EqualTo(2));
        }

        [Test]
        public void ScreenAngle_SidewaysIsFlat_AwayAndTowardsAreVertical()
        {
            Assert.That(WeaponRules.ScreenAngle(Vector2.right, 18f), Is.EqualTo(0f).Within(Tolerance));
            Assert.That(Mathf.Abs(WeaponRules.ScreenAngle(Vector2.left, 18f)), Is.EqualTo(180f).Within(Tolerance));
            Assert.That(WeaponRules.ScreenAngle(Vector2.up, 18f), Is.EqualTo(90f).Within(Tolerance));
            Assert.That(WeaponRules.ScreenAngle(Vector2.down, 18f), Is.EqualTo(-90f).Within(Tolerance));
        }

        [Test]
        public void ScreenAngle_Diagonal_IsFlattenedByThePitch()
        {
            float angle = WeaponRules.ScreenAngle(new Vector2(1f, 1f).normalized, 18f);
            Assert.That(angle, Is.GreaterThan(0f).And.LessThan(45f));
            Assert.That(angle, Is.EqualTo(Mathf.Atan(Mathf.Sin(18f * Mathf.Deg2Rad)) * Mathf.Rad2Deg).Within(Tolerance));
        }
    }
}
