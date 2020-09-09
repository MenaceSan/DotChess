//  
// Copyright (c) 2020 Dennis Robinson (www.menasoft.com). All rights reserved.  
// Licensed under the MIT License. See ReadMe.md file in the project root for full license information.  
// 
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

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
        /// PGN game info headers
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
        public bool TournamentMode; // No changes can be made without reseting the game. TODO. no change to PlayModeId allowed.

        public int MoveCount => Board.State.MoveCount;  // helper.
        public ChessColor TurnColor => Board.State.TurnColor; // helper.

        /// <summary>
        /// Test engines for W and B Best (Computer/AI) moves.
        /// </summary>
        public ChessBestTester TesterW; // My scoring of future moves stored from a previous move.
        public ChessBestTester TesterB;

        const string kLogFileName = "/tmp/DotChess{0}.log";
        static TextWriter LogFile = null;

        public static void DebugLog(string msg)
        {
            // Log debug or error.
            Debug.WriteLine(msg);

            // Log to file?
            if (LogFile == null)
            {
                // open it.
                LogFile = File.CreateText(string.Format(kLogFileName, Process.GetCurrentProcess().Id));
            }
            LogFile?.WriteLine(msg);
        }
        public static void InternalFailure(string msg)
        {
            // Something happened that should never happen!
            DebugLog("INTERNAL FAILURE: " + msg);
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
            TesterW?.Reset();
            TesterB?.Reset();
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
            if (LastResultF.IsComplete() || piece.Color != TurnColor)    // not my turn. weird.
            {
                return new List<ChessMove>();
            }
            return Board.GetValidMovesFor(piece, LastResultF.GetReqInCheck());
        }

        private void MoveAdvance(ChessResultF newFlags)
        {
            var color = TurnColor;
            Info.MoveAdvance();
            LastResultF = newFlags;

            if (newFlags.IsComplete())      // game over.
            {
                Board.State.EnPassantPos = ChessPosition.kNull; // no longer valid.
                TesterW?.Reset();   // no longer valid.
                TesterB?.Reset();   // no longer valid.

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
            // Make a move.
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
            if (piece.Color != TurnColor)    // not my turn. weird.
                return ChessResultF.Invalid;

            string stateString = Board.GetStateString();  // stateString BEFORE this move.  
            ChessTypeId typeId = piece.TypeId;      // type BEFORE promote.

            ChessResultF newFlags = Board.Move(piece, posNew, LastResultF.GetReqInCheck() | (promoteToKnight ? ChessRequestF.PromoteN : 0));
            if (!newFlags.IsAllowedMove())
            {
                return newFlags;    // bad move. no change.
            }

            // Advance any ChessBestTest
            TesterW?.MoveNext(piece.Id, posNew, true);
            TesterB?.MoveNext(piece.Id, posNew, true);

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
            var color = TurnColor;

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
        /// <returns>ChessMoveId</returns>
        public ChessMoveId RecommendBest1()
        {
            ChessRequestF flagsReq = LastResultF.GetReqInCheck() | ChessRequestF.Test;
            bool isWhite = TurnColor == ChessColor.kWhite;
            ChessBestTester tester = (isWhite || TesterB == null) ? TesterW : TesterB;

            if (ChessDb._Instance != null) // find a move in my opening moves db.
            {
                // Always assume the db move is the best move.
                bool transpose = !isWhite;
                List<ChessMoveHistory> dbMoves = ChessDb._Instance.FindMoves(Board.GetHashCode64(transpose), transpose);

                for (int i = 0; i < dbMoves.Count; i++)
                {
                    var move = dbMoves[i];
                    if (!Board.TestMove(GetPiece(move.Id), move.ToPos, flagsReq).IsAllowedMove())
                    {
                        // This should NOT happen. Our db is corrupt? Or a cache collision is suggesting a bad move. Just ignore it.
                        ChessGame.InternalFailure("ChessDb corrupt or cache collision.");
                        dbMoves.RemoveAt(i); i--;
                        continue;
                    }
                }

                if (dbMoves.Count > 0)
                {
                    return dbMoves[tester?.Random.Next(dbMoves.Count) ?? 0];
                }
            }

            if (tester != null)
            {
                // No Db move available. So figure it out.
                tester.FindBestMoves(flagsReq);
                int countMoves = tester.GetBestMovesTieCount();
                if (countMoves <= 0)
                    return null;    // Game over. I have no moves. Checkmate or Stalemate.

                return tester.BestMoves[tester.Random.Next(countMoves)];
            }

            return null;
        }

        /// <summary>
        /// Get proper notation for a move.
        /// </summary>
        public ChessNotationPly GetNotation(ChessMoveId move)
        {
            var piece = GetPiece(move.Id);
            return new ChessNotationPly
            {
                TypeId = piece.TypeId,
                From = piece.Pos,
                Move = move,
            };
        }

        /// <summary>
        /// Play back a move where we already know the outcome.
        /// </summary>
        /// <param name="notation">ChessNotation1</param>
        /// <returns>false = error. the board was not in proper state to play this move.</returns>
        public bool Move(ChessNotationPly notation)
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

            TesterW?.MovePrev(movePrev.Move);
            TesterB?.MovePrev(movePrev.Move);
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
