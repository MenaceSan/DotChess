//  
// Copyright (c) 2020 Dennis Robinson (www.menasoft.com). All rights reserved.  
// Licensed under the MIT License. See ReadMe.md file in the project root for full license information.  
// 
using System.Collections.Generic;
using System.Diagnostics;

namespace DotChess
{
    /// <summary>
    /// Store the current state of a chess game. All PGN data for a game.
    /// Expose functions to play the game. Like https://en.wikipedia.org/wiki/Universal_Chess_Interface
    /// https://opensource.apple.com/source/Chess/Chess-110.0.6/Documentation/PGN-Standard.txt
    /// https://www.expert-chess-strategies.com/chess-notation.html
    /// https://database.chessbase.com/
    /// </summary>
    public class ChessGame
    {
        /// <summary>
        /// PGN headers
        /// </summary>
        public ChessGameInfo Info;

        /// <summary>
        /// PGN History of moves. Length >= Board.State.MoveCount
        /// </summary>
        public List<ChessNotationRev> Moves = new List<ChessNotationRev>();

        /// <summary>
        /// The pieces on the board. Current FEN state.
        /// </summary>
        public ChessGameBoard Board;

        public ChessResultF LastResultF;     // result of the last move = state of the board. (Check, etc)  
        public ChessColorId ResultId2;   // Result from current play. Should match Result for PGN completed games.

        public ChessModeId PlayModeId = ChessModeId.PvP;  // rules enforced? else players can move pieces freely.
        public bool TournamentMode; // No changes can be made without reseting the game. TODO

        public int MoveCount => Board.State.MoveCount;  // helper.

        /// <summary>
        /// Test engines for W and B Best (Computer/AI) moves.
        /// </summary>
        public ChessBestTest TestW; // My scoring of future moves stored from a previous move.
        public ChessBestTest TestB;

        public static void InternalFailure(string msg)
        {
            // Something happened that should never happen!
            Debug.WriteLine("INTERNAL FAILURE: " + msg);
        }

        /// <summary>
        /// Put all pieces back to start.
        /// </summary>
        public void ResetGame()
        {
            Info = new ChessGameInfo();
            Board = new ChessGameBoard();
            Moves = new List<ChessNotationRev>();
            LastResultF = 0;
            ResultId2 = ChessColorId.Undefined;
            PlayModeId = ChessModeId.PvP;       // Wait for players to make moves.
            TestW?.Reset();
            TestB?.Reset();
        }

        public bool IsValidGame()
        {
            string error = Board.GetBoardError();
            if (error != null)
            {
                return false;
            }
            return true;
        }

        public ChessPiece GetPiece(ChessPieceId id)
        {
            return Board.GetPiece(id);
        }

        /// <summary>
        /// Get a list of all legal moves for a piece.
        /// </summary>
        /// <param name="id">ChessPieceId</param>
        /// <returns></returns>
        public List<ChessMove> GetValidMovesFor(ChessPieceId id)
        {
            var piece = GetPiece(id);
            if (LastResultF.IsComplete() || piece.Color != Board.State.TurnColor)    // not my turn. weird.
            {
                return new List<ChessMove>();
            }
            return Board.GetValidMovesFor(piece, LastResultF.GetReqInCheck());
        }

        private void MoveAdvance(ChessResultF newFlags)
        {
            var color = Board.State.TurnColor;
            Info.MoveAdvance();
            LastResultF = newFlags;

            if (newFlags.IsComplete())      // game over.
            {
                Board.State.EnPassantPos = ChessPosition.kNull; // no longer valid.
                TestW?.Reset();   // no longer valid.
                TestB?.Reset();   // no longer valid.

                if (newFlags.IsAny(ChessResultF.Checkmate))
                {
                    ResultId2 = color.Id;
                }
                else if (newFlags.IsAny(ChessResultF.Stalemate))
                {
                    ResultId2 = ChessColorId.Stalemate;
                }
                else
                {
                    ResultId2 = color.Opposite.Id;
                }
            }

            Board.State.MoveAdvance(color);    // advance turn.
        }

        private ChessResultF Move(ChessPiece piece, ChessPosition posNew, bool promoteToKnight = false)
        {
            if (PlayModeId == ChessModeId.NoRules)
            {
                // Allow move without test.
                Board.MoveX(piece, posNew);
                return ChessResultF.OK;
            }

            ChessPosition posOld = piece.Pos;
            if (posOld.Equals(posNew))
                return ChessResultF.Invalid;    // or should we just ignore this ?
            if (LastResultF.IsComplete())    // game over. no moves allowed.
                return LastResultF;
            if (piece.Color != Board.State.TurnColor)    // not my turn. weird.
                return ChessResultF.Invalid;

            string stateString = Board.GetStateString();  // stateString BEFORE this move.  
            ChessTypeId typeId = piece.TypeId;      // type BEFORE promote.

            ChessResultF newFlags = Board.Move(piece, posNew, LastResultF.GetReqInCheck() | (promoteToKnight ? ChessRequestF.PromoteN : 0));
            if (!newFlags.IsAllowedMove())
            {
                return newFlags;    // no move.
            }

            // Advance any ChessBestTest
            TestW?.MoveNext(piece.Id, posNew);
            TestB?.MoveNext(piece.Id, posNew);

            // Complete the move.
            int moveCount = MoveCount;
            MoveAdvance(newFlags);

            // Record History.
            var move = new ChessNotationRev(stateString)
            {
                Move = new ChessMoveId(piece.Id, posNew, newFlags),
                TypeId = typeId,
                From = posOld,
            };

            bool replaceMove = false;

            if (Moves.Count >= moveCount + 1)
            {
                if (move.Equals2(Moves[moveCount]))  // same move. So just keep history the same.
                {
                    replaceMove = true;
                }
                else
                {
                    Moves.RemoveRange(moveCount, Moves.Count - moveCount); // truncate over-written history.
                }
            }
            if (!replaceMove)
            {
                Moves.Add(move);
            }

            return newFlags; // good.
        }

        /// <summary>
        /// Make a move.
        /// </summary>
        /// <param name="id">ChessPieceId</param>
        /// <param name="posNew">ChessPosition</param>
        /// <param name="promoteToKnight">bool</param>
        /// <returns>ChessResultF = result of move. (or failure)</returns>
        public ChessResultF Move(ChessPieceId id, ChessPosition posNew, bool promoteToKnight = false)
        {
            return Move(GetPiece(id), posNew, promoteToKnight);
        }

        /// <summary>
        /// Current Info.MoveColor resigns. This counts as my turn.
        /// </summary>
        /// <param name="isStalemate"></param>
        public void Resign(bool isStalemate)
        {
            var color = Board.State.TurnColor;

            if (isStalemate)
            {
                // We are claiming stalemate. Make sure this is true!
                if (Board.State.IsStalemate)
                {
                    // TODO
                }
            }

            MoveAdvance((isStalemate ? ChessResultF.Stalemate : ChessResultF.Resigned) | color.Flags);

            Debug.Assert(LastResultF.IsComplete());
        }

        /// <summary>
        /// What should my next move be ? best move for me.
        /// </summary>
        /// <param name="depthMax"></param>
        /// <returns>ChessMove</returns>
        public ChessMoveId RecommendBest1()
        {
            ChessRequestF flagsReq = LastResultF.GetReqInCheck();
            bool isWhite = Board.State.TurnColor == ChessColor.kWhite;
            ChessBestTest tester = (isWhite || TestB == null) ? TestW : TestB;

            if (ChessDb._Instance != null) // find a move in my moves db.
            {
                // Always assume the db move is the best move.
                bool transpose = !isWhite;
                List<ChessMoveHistory> dbMoves = ChessDb._Instance.FindMoves(Board.GetHashCode64(transpose), transpose);

                for (int i = 0; i < dbMoves.Count; i++)
                {
                    var move = dbMoves[i];
                    if (!Board.TestMove(GetPiece(move.Id), move.ToPos, flagsReq).IsAllowedMove())
                    {
                        // This should NEVER happen. our db is corrupt! Bad.
                        ChessGame.InternalFailure("ChessDb corrupt.");
                        dbMoves.RemoveAt(i); i--;
                        continue;
                    }
                }

                if (dbMoves.Count > 0)
                {
                    return dbMoves[tester.Random.Next(dbMoves.Count)];
                }
            }

            // No Db move available. So figure it out.
            int depthPrev = tester.InitDepth();
            tester.FindBestMoves(flagsReq); // update scores for BestMoves. This can be VERY slow.
            tester.DepthMax = depthPrev;

            int countMoves = tester.GetBestMovesTieCount();
            if (countMoves <= 0)
                return null;    // Game over. I have no moves. Checkmate or Stalemate.

            return tester.BestMoves[tester.Random.Next(countMoves)];
        }

        /// <summary>
        /// Play back a move where we already know the outcome.
        /// </summary>
        /// <param name="notation">ChessNotation1</param>
        /// <returns>false = error. the board was not in proper state to play this move.</returns>
        public bool Move(ChessNotation1 notation)
        {
            if (!notation.IsValid)
                return false;

            ChessPiece piece = notation.GetPiece(Board, LastResultF.GetReqInCheck());
            if (piece == ChessPiece.kNull)
                return false;

            ChessResultF flags = Move(piece, notation.Move.ToPos, notation.Move.Flags.IsAny(ChessResultF.PromoteN));
            return flags.IsAllowedMove();     // did the move work?
        }

        private void InitResultF()
        {
            // Do we start in check?
            LastResultF = Board.IsInCheck(ChessColor.kWhite) ? ChessResultF.Check : 0;   // white moves first.
        }

        /// <summary>
        /// Turn on/off rules.  
        /// </summary>
        /// <param name="playModeId">ChessModeId.  0 = I can move any piece any place i want. MoveX()</param>
        public bool SetPlayMode(ChessModeId playModeId)
        {
            if (PlayModeId == playModeId)
                return true;

            bool wasNoRules = PlayModeId == ChessModeId.NoRules;
            if (wasNoRules && !IsValidGame()) // Board is invalid !
            {
                return false;
            }

            PlayModeId = playModeId;
            if (wasNoRules)
            {
                // reset game and re-evaluate board state.
                Info.Reset();
                Board.State = new ChessPlayState();
                InitResultF();
            }

            return true;
        }

        /// <summary>
        /// Move back and forth in history.
        /// </summary>
        /// <returns>false = error. the board was not in proper state to play this move.</returns>
        public bool MoveHistory(bool forward)
        {
            int moveCount = MoveCount;

            if (forward)
            {
                // Just play the next move if i have it.
                if (moveCount >= Moves.Count)
                    return false;

                var moveNext = Moves[moveCount];
                string stateString = Board.GetStateString();
                Debug.Assert(moveNext.StateString == stateString);
                return Move(moveNext);
            }

            // reverse last move.
            if (moveCount <= 0)
                return false;

            moveCount--;
            ChessNotationRev movePrev = Moves[moveCount];

            TestW?.MovePrev(movePrev.Move);
            TestB?.MovePrev(movePrev.Move);
            Board = new ChessGameBoard(movePrev.StateString);

            Debug.Assert(this.MoveCount == moveCount);
            return true;
        }

        /// <summary>
        /// Set up a new game board.
        /// </summary>
        /// <param name="info"></param>
        public ChessGame(ChessGameInfo info = null)
        {
            Info = info ?? new ChessGameInfo();
            Board = new ChessGameBoard();
        }

        /// <summary>
        /// Set up a test board.
        /// </summary>
        /// <param name="pieces"></param>
        public ChessGame(IEnumerable<ChessPiece> pieces)
        {
            Info = new ChessGameInfo();
            Board = new ChessGameBoard(pieces);
            InitResultF();
        }
    }
}
