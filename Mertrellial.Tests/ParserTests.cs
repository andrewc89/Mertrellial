using System.Collections.Generic;
using NUnit.Framework;

namespace Mertrellial.Tests
{
    [TestFixture]
    public class ParserTests
    {
        private readonly Dictionary<string, string> _verbs;
        private readonly List<string> _commitMessages;
        private readonly List<Comment> _expectedComments;
        private Parser parser;

        public ParserTests ()
        {
            _verbs = new Dictionary<string, string> { { "coding", "Development" }, { "testing", "Testing" } };
            _commitMessages = new List<string>
            {
                "Mertrellial card 3: added NUnit test library",
                "testing Mertrellial card 3: added ParseCommitMessage() test"
            };
            _expectedComments = new List<Comment>
            {
                new Comment("Mertrellial", 3, "added NUnit test library"),
                new Comment("Mertrellial", 3, "added ParseCommitMessage() test", "Testing")
            };
            parser = new Parser();
        }

        [Test]
        public void ParseCommitMessageWithoutVerb ()
        {
            parser.SetVerbs(_verbs);
            var message = _commitMessages[0];

            var comment = parser.ParseCommitMessage(message)[0];

            var expectedComment = _expectedComments[0];
            Assert.That(comment.BoardName, Is.EqualTo(expectedComment.BoardName));
            Assert.That(comment.CardId, Is.EqualTo(expectedComment.CardId));
            Assert.That(comment.List, Is.EqualTo(expectedComment.List));
            Assert.That(comment.Message, Is.EqualTo(expectedComment.Message));
        }

        [Test]
        public void ParseCommitMessageWithVerb ()
        {
            parser.SetVerbs(_verbs);
            var message = _commitMessages[1];

            var comment = parser.ParseCommitMessage(message)[0];

            var expectedComment = _expectedComments[1];
            Assert.That(comment.BoardName, Is.EqualTo(expectedComment.BoardName));
            Assert.That(comment.CardId, Is.EqualTo(expectedComment.CardId));
            Assert.That(comment.List, Is.EqualTo(expectedComment.List));
            Assert.That(comment.Message, Is.EqualTo(expectedComment.Message));
        }

        [Test]
        public void ParseCommitMessageWithMultipleLines ()
        {
            parser.SetVerbs(_verbs);

            var comments = parser.ParseCommitMessage(string.Join(Constants.NewLine, _commitMessages));

            foreach (var comment in comments)
            {
                var index = comments.IndexOf(comment);
                Assert.That(comment.BoardName, Is.EqualTo(_expectedComments[index].BoardName));
                Assert.That(comment.CardId, Is.EqualTo(_expectedComments[index].CardId));
                Assert.That(comment.List, Is.EqualTo(_expectedComments[index].List));
                Assert.That(comment.Message, Is.EqualTo(_expectedComments[index].Message));
            }
        }

        [Test]
        public void CatchMessageWithoutBoard ()
        {
            parser.SetVerbs(_verbs);
            var message = "testing card 3: testing for poorly formatted commit messages";

            var comments = parser.ParseCommitMessage(message);

            Assert.That(comments.Count, Is.EqualTo(0));
        }

        [Test]
        public void CatchMessageWithoutBoardorVerb ()
        {
            var message = "card 3: testing for poorly formatted commit messages";

            var comments = parser.ParseCommitMessage(message);

            Assert.That(comments.Count, Is.EqualTo(0));
        }
    }
}