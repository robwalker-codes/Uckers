using NUnit.Framework;

namespace Uckers.Tests.Domain.Smoke
{
    public sealed class SmokeTests
    {
        private const int Seed = 42;
        private const int SampleCount = 3;

        [Test]
        public void SeededRandomSequencesMatch()
        {
            var firstSequence = CreateSequence();
            var secondSequence = CreateSequence();

            CollectionAssert.AreEqual(firstSequence, secondSequence);
        }

        private static int[] CreateSequence()
        {
            var generator = new System.Random(Seed);
            var values = new int[SampleCount];

            for (var index = 0; index < SampleCount; index++)
            {
                values[index] = generator.Next();
            }

            return values;
        }
    }
}
