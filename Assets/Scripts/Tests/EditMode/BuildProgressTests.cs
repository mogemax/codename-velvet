using System;
using CodenameVelvet.Inventory;
using NUnit.Framework;

namespace CodenameVelvet.Tests
{
    public class BuildProgressTests
    {
        private static BuildProgress NewProgress() => new BuildProgress(new[]
        {
            new ResourceAmount(ResourceType.Wood, 24),
            new ResourceAmount(ResourceType.Rope, 4),
        });

        [Test]
        public void ToString_PutsThreeMaterialsPerLine()
        {
            var progress = new BuildProgress(new[]
            {
                new ResourceAmount(ResourceType.Wood, 1), new ResourceAmount(ResourceType.Stone, 2),
                new ResourceAmount(ResourceType.Rope, 3), new ResourceAmount(ResourceType.Mineral, 4),
            });

            Assert.That(progress.ToString(), Is.EqualTo("Wood 0/1   Stone 0/2   Rope 0/3\nMineral 0/4"));
        }

        [Test]
        public void TakeFrom_TakesOnlyWhatIsMissing_AndLeavesTheRest()
        {
            var inventory = new ResourceInventory();
            inventory.TryAdd(ResourceType.Wood, 30);
            inventory.TryAdd(ResourceType.Mineral, 3);
            BuildProgress progress = NewProgress();

            Assert.That(progress.TakeFrom(inventory), Is.True);
            Assert.That(inventory.CountOf(ResourceType.Wood), Is.EqualTo(6));
            Assert.That(inventory.CountOf(ResourceType.Mineral), Is.EqualTo(3));
            Assert.That(progress.IsComplete, Is.False);
            Assert.That(progress.ToString(), Is.EqualTo("Wood 24/24   Rope 0/4"));
        }

        [Test]
        public void TakeFrom_SeveralInventories_CompletesIt()
        {
            var first = new ResourceInventory();
            first.TryAdd(ResourceType.Wood, 14);
            var second = new ResourceInventory();
            second.TryAdd(ResourceType.Wood, 10);
            second.TryAdd(ResourceType.Rope, 4);
            BuildProgress progress = NewProgress();

            progress.TakeFrom(first);
            Assert.That(progress.StepsDone(2), Is.EqualTo(1));
            progress.TakeFrom(second);

            Assert.That(progress.IsComplete, Is.True);
            Assert.That(progress.StepsDone(2), Is.EqualTo(2));
        }

        [Test]
        public void TakeFrom_NothingUseful_ReturnsFalse()
        {
            var inventory = new ResourceInventory();
            inventory.TryAdd(ResourceType.Mineral, 5);

            Assert.That(NewProgress().TakeFrom(inventory), Is.False);
        }

        [Test]
        public void Constructor_NonPositiveCost_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new BuildProgress(new[] { new ResourceAmount(ResourceType.Wood, 0) }));
        }
    }
}
