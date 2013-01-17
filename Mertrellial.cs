
namespace Mertrellial
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// updates trello cards via mercurial commits
    /// use: new Mertrellial("repo filepath").CheckCommits()
    /// </summary>
    public class Mertrellial
    {
        /// <summary>
        /// verbs used to move cards between lists
        /// </summary>
        private readonly List<string> VERBS = new List<string> { "developing", "coding", "testing", "waiting", "finishing", "finished" };

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="RepoPath">filepath of repository directory</param>
        /// <param name="AppKey">Trello application key</param>
        /// <param name="AuthToken">Trello authentication token</param>
        public Mertrellial (string RepoPath, string AppKey = "<your app key here>", string AuthToken = "<your auth token here>")
        {
            if (string.IsNullOrEmpty(AppKey) || string.IsNullOrEmpty(AuthToken))
            {
                throw new Exception("You need to specify your Trello application key and auth token");
            }
            Repo = new Mercurial.Repository(RepoPath);
            Trello = new TrelloNet.Trello(AppKey);
            try
            {
                Trello.Authorize(AuthToken);
            }
            catch (Exception) { throw new Exception("Could not connect to Trello.  Perhaps your auth token has expired?"); }
            Comments = new List<Comment>();
        }

        /// <summary>
        /// Mercurial repository
        /// </summary>
        private Mercurial.Repository Repo;
        
        /// <summary>
        /// Trello.NET object
        /// </summary>
        private TrelloNet.Trello Trello;
        
        /// <summary>
        /// recent changesets
        /// </summary>
        private List<Mercurial.Changeset> Commits;
        
        /// <summary>
        /// all commit messages
        /// </summary>
        private List<Comment> Comments;

        /// <summary>
        /// load all commits since specified datetime (if unspecified, since yesterday),
        /// parse their commit messages, push comments up to Trello
        /// </summary>
        /// <param name="Since">check for commits since when? default = yesterday at same time</param>
        public void CheckCommits (DateTime? Since = null)
        {
            if (Since == null)
            {
                Since = DateTime.Now.AddHours(-1);
            }
            Commits = Repo.Log().Where(x => x.Timestamp > Since).ToList();            
            foreach (var Commit in Commits)
            {
                Comments.AddRange(ParseCommitMessage(Commit.CommitMessage));
            }
            PushComments();
        }

        /// <summary>
        /// parse a commit message, following specified syntax
        /// </summary>
        /// <param name="CommitMessage">commit message</param>
        /// <returns>list of comments to push up to Trello</returns>
        private List<Comment> ParseCommitMessage (string CommitMessage)
        {
            var Messages = Regex.Split(CommitMessage, "\r\n|\r|\n");
            var Comments = new List<Comment>();
            foreach (var Message in Messages)
            {
                try
                {
                    var Tokens = Message.Split(' ').ToList();
                    var Comment = new Comment();
                    if (VERBS.Contains(Tokens[0].ToLower()))
                    {
                        var verb = new Verb(Tokens[0]);
                        Comment.Verb = verb;
                        Tokens.RemoveAt(0);
                    }
                    int CardIndex = Tokens.FindIndex(x => x.ToLower().Equals("card"));
                    if (CardIndex < 0) return new List<Comment>();
                    Comment.BoardName = string.Join(" ", Tokens.GetRange(0, CardIndex));
                    Comment.CardId = int.Parse(Regex.Replace(Tokens.ElementAt(CardIndex + 1), "[^0-9]+", string.Empty));
                    Comment.Message = string.Join(" ", Tokens.GetRange(CardIndex + 2, Tokens.Count - CardIndex - 2));
                    Comments.Add(Comment);
                }
                catch (Exception) { Console.WriteLine("Caught poorly formatted message: " + Message); }
            }
            return Comments;
        }
        
        /// <summary>
        /// push up all comments to Trello
        /// groups comments by Board so each Board is only loaded once
        /// </summary>
        private void PushComments ()
        {
            var Boards = Comments.Select(x => x.BoardName).Distinct();
            foreach (var Board in Boards)
            {
                var TrelloBoard = Trello.Boards.Search(Board, 1).First();
                foreach (var Comment in Comments.Where(x => x.BoardName == Board))
                {
                    PushComment(TrelloBoard, Comment);
                }
            }
        }

        /// <summary>
        /// add a single comment to a card on the specified Board
        /// </summary>
        /// <param name="Board">Trello Board</param>
        /// <param name="Comment">Comment object</param>
        private void PushComment (TrelloNet.Board Board, Comment Comment)
        {
            var Cards = Trello.Cards.ForBoard(Board);
            var Card = Cards.Single(x => x.IdShort.Equals(Comment.CardId));
            Trello.Cards.AddComment(Card, Comment.Message);
            if (Comment.Verb != null)
            {
                var List = Trello.Lists.ForBoard(Board).Single(x => x.Name.Equals(Comment.Verb.List));
                Trello.Cards.Move(Card, List);
            }
        }
    }   

    /// <summary>
    /// wraps comment info from commit messages
    /// </summary>
    public class Comment
    {
        public Comment () { }

        public Comment (string Board, int Card, string Message, Verb verb = null)
        {
            this.BoardName = Board;
            this.CardId = Card;
            this.Message = Message;
            if (verb != null)
            {
                this.Verb = verb;
            }
        }

        /// <summary>
        /// Trello Board name
        /// </summary>
        public string BoardName { get; set; }
        
        /// <summary>
        /// Card id
        /// </summary>
        public int CardId { get; set; }
        
        /// <summary>
        /// commit message (to be comment on specified card)
        /// </summary>
        public string Message { get; set; }
        
        /// <summary>
        /// verb specifying Trello List to which to move Card
        /// </summary>
        public Verb Verb { get; set; }
    }

    /// <summary>
    /// translates verbs in commit messages to Trello Lists
    /// </summary>
    public class Verb
    {
        /// <summary>
        /// Trello List name
        /// </summary>
        public readonly string List;

        public Verb (string verb)
        {
            this.List = GetList(verb.ToLower());
        }

        private string GetList (string verb)
        {
            switch (verb.ToLower())
            {
                case "developing": return "Development";
                case "coding": return "Development";
                case "testing": return "Testing";
                case "waiting": return "User Acceptance";
                case "finishing": return "Done";
                case "finished": return "Done";
                default: return "";
            }
        }
    }
}
