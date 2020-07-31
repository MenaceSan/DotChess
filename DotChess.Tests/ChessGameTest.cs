using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;

namespace DotChess.Tests
{
    /// <summary>
    /// a game test instance. So i can parallel this on multiple threads.
    /// </summary>
    public class ChessGameTest
    {
        public string FileName;     // Where did i get the game notation from?
        public int LineNumber;      // Current line of the file.

        public ChessGame Game;
        public List<ChessNotationPly> Notations;      // the moves to play.

        public bool BuildOpeningDb;     // Do special stuff if we are building the opening db.
        ChessColor ColorWinner;         // Only record the winners moves. Don't record loser moves.

        private void TestMoveValid(ChessNotationPly notation)
        {
            // Is this move valid ?
            ChessPiece piece = notation.GetPiece(Game.Board, Game.LastResultF.GetReqInCheck());
            Assert.IsTrue(piece != null);

            List<ChessMove> possibles = Game.GetValidMovesFor(piece.Id);
            Assert.IsTrue(possibles.Any(x => x.ToPos.Equals(notation.Move.ToPos)));
            Assert.IsTrue(Game.IsValidGame());
        }

        private void TestBoardState()
        {
            ulong hash1 = Game.Board.GetHashCode64(false);
            string fen1 = Game.Board.GetFEN(true);
            string stateString1 = Game.Board.GetStateString();

            var boardFromFEN = new ChessBoard(fen1.Split(null), 0, true);    // kFenSep
            var boardFromState = new ChessBoard(stateString1);    // restore form State.

            string fen2 = boardFromFEN.GetFEN(true);
            Assert.IsTrue(fen2 == fen1);

            string stateString2 = boardFromState.GetStateString();
            Assert.IsTrue(stateString1 == stateString2);

            ulong hash2 = boardFromFEN.GetHashCode64(false);
            Assert.IsTrue(hash2 == hash1);

            hash2 = boardFromState.GetHashCode64(false);
            Assert.IsTrue(hash2 == hash1);

            var board3 = new ChessBoard(boardFromFEN);
            Assert.IsTrue(fen2 == board3.GetFEN(true));

            var board4 = new ChessBoard(boardFromState);
            Assert.IsTrue(fen2 == board4.GetFEN(true));
            Assert.IsTrue(stateString2 == board4.GetStateString());
        }

        public void PlayMove(ChessNotationPly notation)
        {
            if (notation.Move.Flags.IsAny(ChessResultF.Resigned | ChessResultF.Stalemate))
            {
                Game.Resign(notation.Move.Flags.IsAny(ChessResultF.Stalemate));
                return;
            }

            // Assert.IsTrue(Game.IsValidGame());
            Assert.IsTrue(notation.IsValid);

            ulong hashCode = 0;

            if (BuildOpeningDb)
            {
                hashCode = Game.Board.GetHashCode64(ColorWinner == ChessColor.kBlack);    // before move.
            }
            else
            {
                TestMoveValid(notation);
                TestBoardState();   // Validate board state BEFORE move.
            }

            // Make the move.
            Assert.IsTrue(Game.Move(notation));

            // Validate board state AFTER move.
            Assert.IsTrue(Game.IsValidGame());

            ChessNotationRev moveLast = Game.Moves.Last();
            Assert.IsTrue(moveLast.Move.Id == notation.Move.Id);
            Assert.IsTrue(moveLast.Move.ToPos.Equals(notation.Move.ToPos));

            ChessPiece piece = Game.GetPiece(notation.Move.Id); // the piece that moved.

            // Games don't always report checkmate.
            if (notation.Move.Flags.IsAny(ChessResultF.Checkmate) != moveLast.Move.Flags.IsAny(ChessResultF.Checkmate))
            {
                // ASSUME Game Over.
                // Why did the game say it was a checkmate but i don't think so ? 
                Assert.IsTrue(!notation.Move.Flags.IsAny(ChessResultF.Checkmate));
                Assert.IsTrue(Game.Board.TestCheckmate(piece.Color));
            }
            if (notation.Move.Flags.IsAny(ChessResultF.Check) != moveLast.Move.Flags.IsAny(ChessResultF.Check))
            {
                // Some PGN games don't record check correctly! (after EnPassant)
                Assert.IsTrue(!notation.Move.Flags.IsAny(ChessResultF.Check));
            }

            // Move must predict a capture correctly.
            const ChessResultF kFlagsMatch = ChessResultF.CastleQ | ChessResultF.CastleK | ChessResultF.Capture | ChessResultF.PromoteQ | ChessResultF.PromoteN;
            Assert.IsTrue((moveLast.Move.Flags & kFlagsMatch) == (notation.Move.Flags & kFlagsMatch));
            const ChessResultF kFlagsIgnore = ChessResultF.EnPassant | ChessResultF.Checkmate | ChessResultF.Check | ChessResultF.Good | ChessResultF.Bad; // | ChessResultF.ColorW | ChessResultF.ColorB
            Assert.IsTrue((moveLast.Move.Flags & ~kFlagsIgnore) == (notation.Move.Flags & ~kFlagsIgnore));

            if (BuildOpeningDb
                && Game.MoveCount < ChessDb.kOpeningMoves
                && ChessDb._Instance != null
                && piece.Color == ColorWinner)
            {
                // Record the move to the opening Db
                ChessDb._Instance.AddGameMove(hashCode, notation.Move, Game.Info, ColorWinner == ChessColor.kBlack);
            }
        }

        public bool PlayTheGame()
        {
            // Test Play the moves.

            if (Game == null)
            {
                Game = new ChessGame();
            }
            else
            {
                ColorWinner = Game.Info.ColorWinner;
            }
            if (BuildOpeningDb && ColorWinner == null)      // I cant do anything if i dont know who wins.
            {
                return false;
            }

            Assert.IsTrue(Game.IsValidGame());

            int moveCount = 0;
            foreach (ChessNotationPly notation in Notations)
            {
                int turnNumber = (moveCount / 2) + 1;
                Assert.IsTrue(Game.Board.State.TurnNumber == turnNumber);

                PlayMove(notation);
                moveCount++;
            }

            return true;
        }

        public void PlayTheGame2()
        {
            bool winningGame = PlayTheGame();
            Assert.IsTrue(Game.IsValidGame());
            if (winningGame)
            {
                Assert.IsTrue(Game.MoveCount == Notations.Count);
                Assert.IsTrue(Game.LastResultF.IsComplete());
            }
        }

        internal static int PlayDir(string dirPath, bool buildOpeningDb)
        {
            // Play all the games in this directory.
            // https://www.pgnmentor.com/files.html

            var listGames = new List<ChessGameTest>(); // games to play test in parallel

            int games = 0;
            var dir = new DirectoryInfo(dirPath);
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string ext = file.Extension.ToLower();
                if (ext == ".zip")
                {
                    // unzip and process.
                    string tempFolder = Path.GetTempFileName();
                    File.Delete(tempFolder);
                    Directory.CreateDirectory(tempFolder);
                    ZipFile.ExtractToDirectory(file.FullName, tempFolder);
                    games += PlayDir(tempFolder, buildOpeningDb);
                    Directory.Delete(tempFolder, true);
                    continue;
                }

                if (ext == ".txt" || ext == ".pgn")
                {
                    // Read PGN and TXT files.
                    string[] lines = File.ReadAllLines(file.FullName);
                    Assert.IsTrue(lines != null);

                    // May have multiple games in the PGN file.
                    int lineNumber = 0;
                    while (lineNumber < lines.Length)
                    {
                        if (string.IsNullOrWhiteSpace(lines[lineNumber]))
                        {
                            lineNumber++;
                            continue;
                        }

                        var gameInfo = new ChessGameInfo();
                        lineNumber = gameInfo.LoadPgn(lines, lineNumber);

                        List<ChessNotationPly> notations = ChessNotationPly.LoadPgn(lines, ref lineNumber);
                        Assert.IsTrue(notations != null);

                        // load up a Batch 
                        listGames.Add(new ChessGameTest
                        {
                            FileName = file.Name,
                            LineNumber = lineNumber,
                            Game = new ChessGame(gameInfo),
                            Notations = notations,
                            BuildOpeningDb = buildOpeningDb,
                        });
                        games++;

                        if (listGames.Count >= ChessUtil.kThreadsMax)
                        {
                            // Run full batch.
                            Parallel.ForEach(listGames, x => x.PlayTheGame2());
                            listGames.Clear();
                        }
                    }
                }
            }

            if (listGames.Count > 0)
            {
                Parallel.ForEach(listGames, x => x.PlayTheGame2());
            }

            return games;
        }
    }

}
