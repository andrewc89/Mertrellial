
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
            if (Repo != null)
            {
                Console.WriteLine("Found repository at " + Repo.Path);
            }
            Trello = new TrelloNet.Trello(AppKey);
            try
            {
                Trello.Authorize(AuthToken);
            }
            catch (Exception) { throw new Exception("Could not connect to Trello.  Perhaps your auth token has expired?"); }
            Console.WriteLine("Connected to Trello as " + Trello.Members.Me().FullName);
            Comments = new List<Comment>();
            Parser = new Parser();
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
        /// responsible for parsing commit messages
        /// </summary>
        private Parser Parser;

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
            Console.WriteLine("Loading commits committed since " + Since.ToString() + "...");
            Commits = Repo.Log().Where(x => x.Timestamp > Since).ToList();            
            foreach (var Commit in Commits)
            {
                Console.WriteLine("Found commit from " + Commit.Timestamp.ToString() + " by " + Commit.AuthorName);
                Comments.AddRange(Parser.ParseCommitMessage(Commit.CommitMessage));
            }
            PushComments();
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
            var Card = Trello.Cards.WithShortId(Comment.CardId, Board);
            Trello.Cards.AddComment(Card, Comment.Message);
            Console.WriteLine("Added comment to card #" + Card.IdShort + " on the " + Board.Name + " board");
            if (Comment.List != null)
            {
                var List = Trello.Lists.ForBoard(Board).Single(x => x.Name.Equals(Comment.List));
                Trello.Cards.Move(Card, List);
            }
        }

        /// <summary>
        /// updates VERBS dictionary
        /// </summary>
        /// <param name="Verbs">Dictionary with which to update VERBS</param>
        public void SetVerbs (Dictionary<string,string> Verbs)
        {
            Parser.SetVerbs(Verbs);
        }
    }

    public class Parser
    {
        public Parser () { }

        /// <summary>
        /// maps verbs to Trello lists for moving cards
        /// </summary>
        private Dictionary<string, string> VERBS = new Dictionary<string, string> 
        { 
            { "developing", "Development" },
            { "coding", "Development" },
            { "testing", "Testing" },
            { "waiting", "User Acceptance" },
            { "finishing", "Done" },
            { "finished", "Done" }
        };

        /// <summary>
        /// updates VERBS dictionary
        /// </summary>
        /// <param name="Verbs">Dictionary with which to update VERBS</param>
        public void SetVerbs (Dictionary<string, string> Verbs)
        {
            VERBS = Verbs;
        }

        /// <summary>
        /// parse a commit message, following specified syntax
        /// </summary>
        /// <param name="CommitMessage">commit message</param>
        /// <returns>list of comments to push up to Trello</returns>
        public List<Comment> ParseCommitMessage (string CommitMessage)
        {
            var Messages = Regex.Split(CommitMessage, "\r\n|\r|\n");
            var Comments = new List<Comment>();
            foreach (var Message in Messages)
            {
                try
                {
                    var Tokens = Message.Split(' ').ToList();
                    var Comment = new Comment();
                    var Verb = Tokens[0].ToLower();
                    if (VERBS.Keys.Contains(Verb))
                    {
                        Comment.List = VERBS[Verb];
                        Tokens.RemoveAt(0);
                    }
                    int CardIndex = Tokens.FindIndex(x => x.ToLower().Equals("card"));
                    if (CardIndex < 0) return new List<Comment>();
                    Comment.BoardName = string.Join(" ", Tokens.GetRange(0, CardIndex));
                    if (string.IsNullOrEmpty(Comment.BoardName.Trim()) || Comment.BoardName.Equals("card"))
                    {
                        break;
                    }
                    Comment.CardId = int.Parse(Regex.Replace(Tokens.ElementAt(CardIndex + 1), "[^0-9]+", string.Empty));
                    Comment.Message = string.Join(" ", Tokens.GetRange(CardIndex + 2, Tokens.Count - CardIndex - 2)).Trim();
                    Comments.Add(Comment);
                }
                catch (Exception) { Console.WriteLine("Caught poorly formatted message: " + Message); }
            }
            return Comments;
        }
    }

    /// <summary>
    /// wraps comment info from commit messages
    /// </summary>
    public class Comment
    {
        public Comment () { }

        public Comment (string Board, int Card, string Message, string List = null)
        {
            this.BoardName = Board;
            this.CardId = Card;
            this.Message = Message;
            if (!string.IsNullOrEmpty(List))
            {
                this.List = List;
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
        /// Trello list to which to move the card
        /// </summary>
        public string List { get; set; }
    }    
}
