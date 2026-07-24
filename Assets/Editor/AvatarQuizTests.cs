#if UNITY_INCLUDE_TESTS
using System.Collections.Generic;
using CaveGame.Quiz;
using NUnit.Framework;

public sealed class AvatarQuizTests
{
    [Test]
    public void ThresholdTriggersOnceAtEveryStep()
    {
        Assert.IsFalse(AvatarQuizController.ShouldStartAtScore(999, 1000, false));
        Assert.IsTrue(AvatarQuizController.ShouldStartAtScore(1000, 1000, false));
        Assert.IsFalse(AvatarQuizController.ShouldStartAtScore(1000, 1000, true));
        Assert.IsTrue(AvatarQuizController.ShouldStartAtScore(2000, 2000, false));
    }

    [Test]
    public void DeckRepeatsOnlyAfterACompletePass()
    {
        var questions = new[]
        {
            Q("A", "1"), Q("B", "2"), Q("C", "3")
        };
        var deck = new QuizQuestionDeck(questions, 42);
        var seen = new HashSet<QuizQuestion> { deck.Next(), deck.Next(), deck.Next() };
        Assert.AreEqual(3, seen.Count);
        Assert.NotNull(deck.Next());
    }

    [TestCase("  PARIS! ", true)]
    [TestCase("Die Antwort ist Paris.", true)]
    [TestCase("Berlin", false)]
    [TestCase("", false)]
    public void LocalFallbackNormalizesAnswers(string transcript, bool expected)
    {
        Assert.AreEqual(expected, LocalAnswerMatcher.IsMatch(transcript, Q("Hauptstadt?", "Paris")));
    }

    private static QuizQuestion Q(string question, string answer)
    {
        return new QuizQuestion { question = question, expectedAnswer = answer, acceptedVariants = new List<string>() };
    }
}
#endif
