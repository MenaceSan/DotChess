using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace DotChess.Tests
{
    [TestClass()]
    public class ChessTests
    {
        public TestContext TestContext { get; set; }    // WriteLine . Console ? or Debug ?

        private string GetDir()
        {
            // Find the directory with our test files.

            return @"C:\FourTe\Dot\DotChess";
        }

        [TestMethod()]
        public void TestChessNotation()
        {
            // Test specific notations.

            var note = new ChessNotationPly();
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
                Assert.IsTrue(ChessPosition.GetX(ChessPosition.GetNotationX(i)) == i);
                Assert.IsTrue(ChessPosition.GetY(ChessPosition.GetNotationY(i)) == i);
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
            List<ChessNotationPly> notations2 = ChessNotationPly.LoadPgn(_ChessMoves2, ref lineNumber);
            Assert.IsTrue(notations2 != null);

            lineNumber = 0;
            List<ChessNotationPly> notations1 = ChessNotationPly.LoadPgn(_ChessMoves1, ref lineNumber);
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
            List<ChessNotationPly> notations = ChessNotationPly.LoadPgn(_ChessMoves1, ref lineNumber);
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
            int games = ChessGameTest.PlayDir(Path.Combine(GetDir(), "Games"), false);   // Openings, Players, Games or Todo
            Console.WriteLine($"Played {games} Games from Games Dir.");
            Assert.IsTrue(games > 0);
        }

        // [TestMethod()]      // This can take a long time, disable it normally.
        public void TestChessPlayersDir()
        {
            // load a bunch of PGN notation games and play them.
            // NOTE: this may be slow.
            int games = ChessGameTest.PlayDir(Path.Combine(GetDir(), "Players"), false);   // Openings, Players, Games or Todo
            Console.WriteLine($"Played {games} Games from Players Dir.");
            Assert.IsTrue(games > 0);
        }

        // [TestMethod()]      // Enable this only when needed. Remove this when not needed.
        public void BuildOpeningDbFile()
        {
            // Read all the games i have and record the first 10 moves to the chessDb.
            // NOTE: this may be slow.
            // ChessDb.OpenDbFile(kDir);
            ChessDb.OpenDbFile("");

            // Play games and build db.
            int games = ChessGameTest.PlayDir(Path.Combine(GetDir(), "Games"), true);
            games += ChessGameTest.PlayDir(Path.Combine(GetDir(), "Players"), true);
            games += ChessGameTest.PlayDir(Path.Combine(GetDir(), "Openings"), true);

            // Write out the db file.
            ChessDb.WriteDbFile(GetDir());
            Console.WriteLine($"ChessDb.WriteDbFile {games} Games from Dirs.");
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

            var board2 = new ChessGameBoard(new string[] { fen1t }, 0, true);
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

            // Unbalanced game. Black should win! White should lose.
            game.TesterW = new ChessBestTester(game.Board, 1, new Random(212121212), CancellationToken.None);       // set a constant seed.
            game.TesterB = new ChessBestTester(game.Board, 2, new Random(212121212), CancellationToken.None);

            ChessDb.OpenDbFile(GetDir());

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

            int testCount = game.TesterW.TestCount + game.TesterB.TestCount;
            TimeSpan timeDiff = DateTime.Now - timeStart;

            Assert.IsTrue(game.IsValidGame());
            Assert.IsTrue(game.MoveCount > 4);
            Assert.IsTrue(game.LastResultF.IsComplete());
            Assert.IsTrue(game.ResultId2 == ChessColorId.Black || game.ResultId2 == ChessColorId.Stalemate);  // Black wins.  White lose.

            Console.WriteLine($"Played recommended Game with {game.MoveCount} moves and {testCount} tests in {timeDiff.TotalSeconds} seconds.");
            Console.WriteLine($"Result: {game.ResultId2}");
        }

        [TestMethod()]
        public void TestChessUci()
        {
            // Test UCI
            // see sample in: http://wbec-ridderkerk.nl/html/UCIProtocol.html

            var retStream = new MemoryStream();
            var uci = new ChessUci(new StreamWriter(retStream) { AutoFlush = true });

            ChessUciRet retCmd = uci.Command("uci");
            Assert.IsTrue(retCmd == ChessUciRet.Ok);

            string ret = Encoding.ASCII.GetString(retStream.ToArray());
            retStream.SetLength(0); // reset.
            Assert.IsTrue(ret.Length > 10); // should return a bunch of stuff.
            Assert.IsTrue(ret.Contains(ChessUci.kOut_uciok));

            retCmd = uci.Command("setoption name Hash value 32");
            Assert.IsTrue(retCmd == ChessUciRet.Ok);
            retCmd = uci.Command("setoption name NalimovCache value 1");
            Assert.IsTrue(retCmd == ChessUciRet.UnkArg);
            retCmd = uci.Command("setoption name NalimovPath value d:\tb; c\tb");
            Assert.IsTrue(retCmd == ChessUciRet.UnkArg);

            retCmd = uci.Command("isready");
            Assert.IsTrue(retCmd == ChessUciRet.Ok);
            ret = Encoding.ASCII.GetString(retStream.ToArray());
            retStream.SetLength(0); // reset.
            Assert.IsTrue(ret.Contains(ChessUci.kOut_readyok));

            retCmd = uci.Command("ucinewgame");
            Assert.IsTrue(retCmd == ChessUciRet.Ok);

            retCmd = uci.Command("setoption name UCI_AnalyseMode value true");
            Assert.IsTrue(retCmd == ChessUciRet.Ok);

            retCmd = uci.Command("position startpos moves e2e4 e7e5");
            Assert.IsTrue(retCmd == ChessUciRet.Ok);
            retCmd = uci.Command("go infinite");
            Assert.IsTrue(retCmd == ChessUciRet.Ok);

            Thread.Sleep(1000); // Wait for 1 second.

            retCmd = uci.Command("stop");
            Assert.IsTrue(retCmd == ChessUciRet.Ok);

            // should return "bestmove g1f3 ponder d8f6"
            ret = Encoding.ASCII.GetString(retStream.ToArray());
            retStream.SetLength(0); // reset.
            Assert.IsTrue(ret.Contains(ChessUci.kOut_bestmove));

            // done.
            retCmd = uci.Command("quit");
            Assert.IsTrue(retCmd == ChessUciRet.Quit);
        }

    }
}
