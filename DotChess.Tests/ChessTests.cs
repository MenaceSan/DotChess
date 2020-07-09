using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DotChess.Tests
{
    /// <summary>
    /// a game test instance. So i can parallel this on multiple threads.
    /// </summary>
    public class ChessGameTest
    {
        public string FileName;     // Where did i get the game notation from?
        public int LineNumber;

        public ChessGame Game;
        public List<ChessNotation1> Notations;      // the moves to play.
        ChessColor ColorWinner;

        public bool BuildOpeningDb;     // Do special stuff if we are building the opening db.

        private void TestMoveValid(ChessNotation1 notation)
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

            var boardFromFEN = new ChessBoard(fen1.Split(null));    // kFenSep
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

        public void PlayMove(ChessNotation1 notation)
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
            foreach (ChessNotation1 notation in Notations)
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

                        List<ChessNotation1> notations = ChessNotation1.LoadPgn(lines, ref lineNumber);
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

    [TestClass()]
    public class ChessTests
    {
        const string kDir = @"C:\FourTe\Dot\DotChess";

        [TestMethod()]
        public void TestChessNotation()
        {
            // Test specific notations.

            var note = new ChessNotation1();
            note.SetNotation("Nfxd2", 0, ChessColor.kWhite);
            Assert.IsTrue(note.IsValid);
        }

        [TestMethod()]
        public void TestChessPosition()
        {
            // Test ChessPosition
            // ChessPieceId.QTY or ChessPosition.kNullVal

            for (byte i = 0; i < (byte)ChessPieceId.QTY; i++)
            {
                Assert.IsTrue(i != ChessPosition.kNullVal);
                var pos = new ChessPosition(i, i);
                Assert.IsTrue(ChessPosition.GetX(ChessPosition.GetLetterX(i)) == i);
                Assert.IsTrue(ChessPosition.GetY(ChessPosition.GetCharY(i)) == i);
                Assert.IsTrue(pos.IsOnBoard == (i < ChessPosition.kDim));
            }
        }

        [TestMethod()]
        public void TestChessReset()
        {
            var game = new ChessGame();
            Assert.IsTrue(game.IsValidGame());

            const string kFEN0 = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
            Assert.IsTrue(game.Board.FEN == kFEN0);

            ulong hash1 = game.Board.GetHashCode64(false);    // new board hash.
            ulong hash2 = game.Board.GetHashCode64(true);    // new board hash should be the same transposed.
            Assert.IsTrue(hash1 == ChessBoard.kHash0);
            // Assert.IsTrue(hash2 == ChessBoard.kHash0);

            game.ResetGame();
            Assert.IsTrue(game.IsValidGame());
            Assert.IsTrue(game.Board.FEN == kFEN0);

            List<ChessMove> moves = game.GetValidMovesFor(ChessPieceId.WK);
            Assert.IsTrue(moves.Count == 0);
            Assert.IsTrue(game.IsValidGame());

            moves = game.GetValidMovesFor(ChessPieceId.WQN);   // Knight has 2 opening moves.
            Assert.IsTrue(moves.Count == 2);
            Assert.IsTrue(game.IsValidGame());

            moves = game.GetValidMovesFor(ChessPieceId.WPg);
            Assert.IsTrue(moves.Count == 2);
            Assert.IsTrue(game.IsValidGame());

            Assert.IsTrue(game.Board.State.IsWhiteTurn);
            Assert.IsTrue(game.Board.State.TurnNumber == 1);
            Assert.IsTrue(game.IsValidGame());

            // Make 1 move.
            game.Move(ChessPieceId.WPg, moves[0].ToPos);
            Assert.IsTrue(!game.Board.State.IsWhiteTurn);
            Assert.IsTrue(game.Board.State.TurnNumber == 1);
        }

        static readonly ChessPiece[] _samplePieces1 = new ChessPiece[]
        {
            new ChessPiece(ChessPieceId.BQ, new ChessPosition('d','8')),
            new ChessPiece(ChessPieceId.BK, new ChessPosition('e','8')),

            new ChessPiece(ChessPieceId.WQ, new ChessPosition('d','2')),   // allowed castle.
            new ChessPiece(ChessPieceId.WK, new ChessPosition('e','1')),
            new ChessPiece(ChessPieceId.WQR, new ChessPosition('a','1')),
            new ChessPiece(ChessPieceId.WKR, new ChessPosition('h','1')),
        };

        [TestMethod()]
        public void TestChessCastle()
        {
            // Castle is such a weird move. Test it special.

            var game = new ChessGame(_samplePieces1);
            Assert.IsTrue(game.IsValidGame());

            string castleStr = "";
            foreach (char ch in ChessUtil.kCastleAvailChar)     // All possible ChessCastleFlags.
            {
                ChessCastleFlags flagsC = ChessUtil.GetCastleFlags(ch);
                char chC = ChessUtil.GetCastleAvailChar(flagsC);
                Assert.IsTrue(flagsC == ChessUtil.GetCastleFlags(chC));
                castleStr += chC;
            }

            Assert.IsTrue(castleStr == ChessUtil.kCastleAvailChar);

            ChessPiece pieceWK = game.GetPiece(ChessPieceId.WK);

            // Is castle allowed?
            List<ChessMove> moves = game.GetValidMovesFor(ChessPieceId.WK);
            Assert.IsTrue(moves.Count == 6);
            Assert.IsTrue(moves.Any(x => x.Flags.IsAny(ChessResultF.CastleK)));
            Assert.IsTrue(moves.Any(x => x.Flags.IsAny(ChessResultF.CastleQ)));
            Assert.IsTrue(game.IsValidGame());

            // Invalidate the castle for the WKR
            Assert.IsTrue(game.Board.State.IsWhiteTurn);
            ChessResultF flagsRes = game.Move(ChessPieceId.WKR, new ChessPosition('h', '3'));
            Assert.IsTrue(flagsRes.IsAllowedMove());
            Assert.IsTrue(game.IsValidGame());

            flagsRes = game.Move(ChessPieceId.BK, new ChessPosition('f', '8'));
            Assert.IsTrue(flagsRes.IsAllowedMove());
            Assert.IsTrue(game.IsValidGame());

            // Next move.
            Assert.IsTrue(game.Board.State.IsWhiteTurn);
            moves = game.GetValidMovesFor(ChessPieceId.WK);
            Assert.IsTrue(!moves.Any(x => x.Flags.IsAny(ChessResultF.CastleK)));
            Assert.IsTrue(moves.Any(x => x.Flags.IsAny(ChessResultF.CastleQ)));
            Assert.IsTrue(game.IsValidGame());
            Assert.IsTrue(moves.Count == 5);
            flagsRes = game.Move(ChessPieceId.WQ, new ChessPosition('d', '3'));
            Assert.IsTrue(flagsRes.IsAllowedMove());

            // I cant castle if I'm in check!
            flagsRes = game.Move(ChessPieceId.BQ, new ChessPosition('e', '7'));  // check.
            Assert.IsTrue(flagsRes.IsAllowedMove());
            Assert.IsTrue(flagsRes.IsAny(ChessResultF.Check));

#if false

            // Make sure it is disallowed in case when i would be put in check.
            moves = game.GetValidMovesFor(ChessPieceId.WK);
            Assert.IsTrue(moves.Count == 4);
            Assert.IsTrue(!moves.Any(x => ChessPiece.HasAnyFlag(x.Flags, ChessFlags.CastleK | ChessFlags.CastleQ)));

            flags = game.Move(ChessPieceId.WQ, new ChessPosition('e', '2'));    // block check.
            Assert.IsTrue(ChessFlagU.IsAllowedMove(flags));
            flags = game.Move(ChessPieceId.BQ, new ChessPosition('d', '8'));    // uncheck.
            Assert.IsTrue(ChessFlagU.IsAllowedMove(flags));

            // Now castle.
            flags = game.Move(ChessPieceId.WKR, new ChessPosition(6, 0));

            flags = game.Move(ChessPieceId.BQ, new ChessPosition(4, 3));

            moves = game.GetValidMovesFor(ChessPieceId.WK);
            Assert.IsTrue(moves.Count > 1);
#endif

        }

        static readonly string[] _ChessMoves1 = // a game in notation.
        {
            "f2-f4 e7-e5",
            "f4xe5 d7-d6",
            "e5xd6 Bf8xd6",
            "g2-g3 Qd8-g5",
            "Ng1-f3 Qg5xg3+",
            "h2xg3 Bd6xg3#",
        };

        static readonly string[] _ChessMoves2 =     // same game in short hand notation.
        {
            "f4 e5", //   The White pawn moves to f4 and the Black pawn to e5.
            "fxe5 d6",  //The White pawn on the f file takes the pawn on e5. The Black pawn moves to d6.
            "exd6 Bxd6",  //  The White pawn on the e file takes the pawn on d6. The Black Bishop takes the pawn on d6.
            "g3 Qg5",   //The White pawn moves to g3. The Black Queen moves to g5.
            "Nf3 Qxg3+",  //  The White Knight moves to f3. The Black Queen takes the pawn on g3 and checks the White King.
            "hxg3 Bxg3#", //
        };


        [TestMethod()]
        public void TestChessMoves()
        {
            // Load and play a notated game.

            int lineNumber = 0;
            List<ChessNotation1> notations2 = ChessNotation1.LoadPgn(_ChessMoves2, ref lineNumber);
            Assert.IsTrue(notations2 != null);

            lineNumber = 0;
            List<ChessNotation1> notations1 = ChessNotation1.LoadPgn(_ChessMoves1, ref lineNumber);
            Assert.IsTrue(notations1 != null);

            var game1 = new ChessGameTest
            {
                Notations = notations1,
            };
            game1.PlayTheGame2();
            Assert.IsTrue(game1.Game.Board.Score == 8 * ChessType.kValuePawn);  // White scores.
            Assert.IsTrue(game1.Game.ResultId2 == ChessColorId.Black);  // Black wins.

            var game2 = new ChessGameTest
            {
                Notations = notations2,
            };
            game2.PlayTheGame2();
            Assert.IsTrue(game1.Game.Board.Score == 8 * ChessType.kValuePawn);  // White scores.
            Assert.IsTrue(game1.Game.ResultId2 == ChessColorId.Black);  // Black wins.

            Assert.IsTrue(Enumerable.SequenceEqual(notations1, notations2));
        }

        [TestMethod()]
        public void TestChessHistory()
        {
            // Try going forward and backward in a game history.

            int lineNumber = 0;
            List<ChessNotation1> notations = ChessNotation1.LoadPgn(_ChessMoves1, ref lineNumber);
            Assert.IsTrue(notations != null);

            var game1 = new ChessGameTest
            {
                Game = new ChessGame(),
                Notations = notations,
            };

            game1.PlayMove(game1.Notations[0]);
            game1.PlayMove(game1.Notations[1]);
            game1.PlayMove(game1.Notations[2]);
            Assert.IsTrue(game1.Game.MoveCount == 3);    // no forward allowed.

            bool ret = game1.Game.MoveHistory(true);
            Assert.IsTrue(!ret);    // no forward allowed.

            ret = game1.Game.MoveHistory(true);  
            Assert.IsTrue(!ret);   // still no forward allowed.

            ret = game1.Game.MoveHistory(false);    // back
            Assert.IsTrue(ret);   // back

            ret = game1.Game.MoveHistory(true);
            Assert.IsTrue(ret);   // forward allowed.
            ret = game1.Game.MoveHistory(true);
            Assert.IsTrue(!ret);    // no forward allowed.


        }

        [TestMethod()]
        public void TestChessGamesDir()
        {
            // load a bunch of PGN notation games and play them.
            int games = ChessGameTest.PlayDir(kDir + @"\Games", false);   // Openings, Players, Games or Todo
            Debug.WriteLine($"Played {games} Games from Games Dir.");
            Assert.IsTrue(games > 0);
        }

        // [TestMethod()]      // This can take a long time, disable it normally.
        public void TestChessPlayersDir()
        {
            // load a bunch of PGN notation games and play them.
            int games = ChessGameTest.PlayDir(kDir + @"\Players", false);   // Openings, Players, Games or Todo
            Debug.WriteLine($"Played {games} Games from Players Dir.");
            Assert.IsTrue(games > 0);
        }

        // [TestMethod()]      // Enable this only when needed. Remove this when not needed.
        public void BuildOpeningDbFile()
        {
            // Read all the games i have and record the first 10 moves to the chessDb.

            // ChessDb.OpenDbFile(kDir);
            ChessDb.OpenDbFile("");

            // Play games and build db.
            int games = ChessGameTest.PlayDir(kDir + @"\Games", true);
            games += ChessGameTest.PlayDir(kDir + @"\Players", true);
            games += ChessGameTest.PlayDir(kDir + @"\Openings", true);

            // Write out the db file.
            ChessDb.WriteDbFile(kDir);
            Debug.WriteLine($"ChessDb.WriteDbFile {games} Games from Dirs.");
        }

        [TestMethod()]
        public void TestHashCode()
        {
            // Make sure transposed FEN is the same as transposed hashCode.

            var game1 = new ChessGame(_samplePieces1);
            Assert.IsTrue(game1.IsValidGame());
            ChessBoard board1 = game1.Board;
            board1.State.White.CastleFlags |= ChessCastleFlags.All;   // Clear these because they mess up perfect transpose. King would no longer be in proper init pos if it originally was.

            ulong hashCode1 = board1.GetHashCode64(false);
            ulong hashCode1t = board1.GetHashCode64(true);

            string[] fen = board1.FEN.Split(null);

            var sb = new StringBuilder();
            board1.GetFEN1(sb);
            string fen1 = sb.ToString();
            ChessBoard.TransposeFEN1(sb);
            string fen1t = sb.ToString();

            var board2 = new ChessGameBoard(new string[] { fen1t });
            Assert.IsTrue(board2.GetBoardError() == null);

            string fen2 = board2.FEN;
            ulong hashCode2 = board2.GetHashCode64(false);
            ulong hashCode2t = board2.GetHashCode64(true);

            Assert.IsTrue(hashCode2 == hashCode1t);
            Assert.IsTrue(hashCode1 == hashCode2t);
        }

        [TestMethod()]
        public void TestChessRecommend()
        {
            // Play a game with all recommended moves.

            var game = new ChessGame();
            Assert.IsTrue(game.IsValidGame());

            // Unbalanced game. Black should win! White lose.
            game.TestW = new ChessBestTest(game.Board, 2, new Random(212121212), CancellationToken.None);       // set a constant seed.
            game.TestB = new ChessBestTest(game.Board, 3, new Random(212121212), CancellationToken.None);

            ChessDb.OpenDbFile(kDir);

            DateTime timeStart = DateTime.Now;
            while (!game.LastResultF.IsComplete())
            {
                if (game.Board.State.IsStalemate)
                {
                    game.Resign(true);
                    break;
                }
                if (game.MoveCount >= ChessPlayState.kMovesMax)
                {
                    game.Resign(false);
                    break;
                }

                ChessMoveId move1 = game.RecommendBest1();
                if (move1 == null)
                {
                    game.Resign(true);  // Game over. no moves.
                    break;
                }

                ChessResultF flags = game.Move(move1.Id, move1.ToPos);
                Assert.IsTrue(flags.IsAllowedMove());
                Assert.IsTrue(game.IsValidGame());
            }

            int testCount = game.TestW.TestCount + game.TestB.TestCount;
            TimeSpan timeDiff = DateTime.Now - timeStart;

            Assert.IsTrue(game.IsValidGame());
            Assert.IsTrue(game.MoveCount > 4);
            Assert.IsTrue(game.LastResultF.IsComplete());
            Assert.IsTrue(game.ResultId2 == ChessColorId.Black || game.ResultId2 == ChessColorId.Stalemate);  // Black wins.  White lose.

            Debug.WriteLine($"Played recommended Game with {game.MoveCount} moves and {testCount} tests in {timeDiff.TotalSeconds} seconds.");
            Debug.WriteLine($"Result: {game.ResultId2}");
        }
    }
}
