//  
// Copyright (c) 2020 Dennis Robinson (www.menasoft.com). All rights reserved.  
// Licensed under the MIT License. See ReadMe.md file in the project root for full license information.  
// 
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace DotChess
{
    /// <summary>
    /// The status of the chess board. Validate chess game moves that might be made.
    /// </summary>
    public class ChessGameBoard : ChessBoard
    {
        public int Score;            // 0 = balanced game, + = white advantage. update this if ChessResultF.Capture or ChessResultF.Promote.

        /// <summary>
        /// re-calculate the minimum ChessCastleFlags
        /// </summary>
        /// <param name="color">ChessColor</param>
        /// <returns>ChessCastleFlags</returns>
        private ChessCastleFlags GetCastleFlagsMin(ChessColor color)
        {
            ChessCastleFlags flags = 0;
            if (!GetPiece(color.KingId).IsInitPos)
                flags |= ChessCastleFlags.All;
            if (!GetPiece(color.RookQ).IsInitPos)
                flags |= ChessCastleFlags.Q;
            if (!GetPiece(color.RookK).IsInitPos)
                flags |= ChessCastleFlags.K;
            return flags;
        }

        /// <summary>
        /// re-calculate the score.
        /// </summary>
        /// <returns></returns>
        private int GetScore()
        {
            int score = 0;
            foreach (ChessPiece piece in Pieces)
            {
                if (piece.IsOnBoard)    // value of my pieces on the board. Not IsCaptured.
                    score += piece.Value * piece.Color.ScoreDir;
            }
            return score;
        }

        /// <summary>
        /// Is the game board state valid? 
        /// </summary>
        /// <returns>Error Message "Problem: X" or null (OK)</returns>

        public string GetBoardError()
        {
            string err = GetGridError();
            if (err != null)
                return err;

            err = GetPiecesError();
            if (err != null)
                return err;

            if (!GetPiece(ChessPieceId.WKB).Pos.IsValidBishop(true))
                return "WKB square color";
            if (!GetPiece(ChessPieceId.WQB).Pos.IsValidBishop(false))
                return "WQB square color";
            if (!GetPiece(ChessPieceId.BKB).Pos.IsValidBishop(false))
                return "BKB square color";
            if (!GetPiece(ChessPieceId.BQB).Pos.IsValidBishop(true))
                return "BQB square color";

            int score2 = GetScore();
            if (Score != score2)
                return "Score Wrong";

            ChessCastleFlags castleFlagsMin = GetCastleFlagsMin(ChessColor.kWhite);     // must have AT LEAST this.
            if ((State.White.CastleFlags & castleFlagsMin) != castleFlagsMin)
                return "CastleFlagsMin W";
            castleFlagsMin = GetCastleFlagsMin(ChessColor.kBlack);     // must have AT LEAST this.
            if ((State.Black.CastleFlags & castleFlagsMin) != castleFlagsMin)
                return "CastleFlagsMin B";

            if (State.EnPassantPos.IsOnBoard)
            {
                // EnPassant Pawn MUST be in correct place.
                ChessColor colorTurn = State.TurnColor;
                ChessPiece piecePawn = GetAt(State.EnPassantPos.X, (byte)(State.EnPassantPos.Y - colorTurn.DirY));
                byte rank = colorTurn.GetRank(State.EnPassantPos);
                if (piecePawn == ChessPiece.kNull || !piecePawn.IsPawnType || rank != ChessPosition.kDim3)
                    return "EnPassant Pos wrong";
            }

            return null;        // OK
        }

        private bool IsCastleable(ChessColor color, bool isQueenSide)
        {
            // Can i do a castle? Assume the King IsPosInit.
            // MUST not be in check now! ASSUME already checked that.
            // https://en.wikipedia.org/wiki/Castling

            var stateColor = State.GetStateColor(color);
            if (stateColor.CastleFlags.IsAny(ChessUtil.GetCastleFlags(isQueenSide)))     // already moved 
                return false;

            Debug.Assert(GetPiece(color.KingId).IsInitPos);

            ChessPiece rook = GetPiece(color.GetRookId(isQueenSide));
            if (!rook.IsInitPos)
            {
                // CastleFlags FAILED ME? Should not happen!
                return false;
            }

            // Must have empty spaces from rook to king.
            byte x1, x2;
            if (isQueenSide)
            {
                x1 = 1; x2 = ChessPosition.kXK;
            }
            else
            {
                x1 = ChessPosition.kXK + 1; x2 = ChessPosition.kDim1;
            }

            byte y0 = color.RowKing;
            for (byte x = x1; x < x2; x++)
            {
                if (!Grid.IsEmpty(x, y0))
                    return false;
            }

            return true;    // castle is allowed. Assume move will revert if this puts me in check.
        }

        private ChessResultF TestMove1(ChessPiece piece, ChessPosition posNew, ChessRequestF flagsReq)
        {
            // Can i go on this space? No check of distance/path moved. 

            if (!posNew.IsOnBoard)  // can't move off the board.
                return ChessResultF.Invalid;
            Debug.Assert(!piece.Pos.Equals(posNew));

            ChessPiece pieceCapture = GetAt(posNew);
            ChessPosition posOld = piece.Pos;

            ChessResultF flags;
            if (pieceCapture == ChessPiece.kNull)
            {
                if (piece.IsPawnType)
                {
                    // Can I make EnPassant capture of a pawn?
                    if (posNew.Equals(State.EnPassantPos))
                    {
                        Debug.Assert(State.EnPassantPos.Y == 2 || State.EnPassantPos.Y == ChessPosition.kDim3);
                        return ChessResultF.EnPassant | ChessResultF.Capture;
                    }
                    if (posOld.X != posNew.X)  // pawn is blocked diagonal. Must capture.
                        return ChessResultF.Invalid;
                }
                flags = ChessResultF.OK;
            }
            else
            {
                if (pieceCapture.Color == piece.Color)  // cant capture my own side
                    return ChessResultF.Invalid; // Blocked
                if (piece.IsPawnType && piece.Pos.X == posNew.X)  // must capture to move diagonal.
                    return ChessResultF.Invalid; // Blocked
                flags = ChessResultF.Capture;
            }

            if (piece.IsPawnType && piece.Color.GetRank(posNew) == ChessPosition.kDim1)    // Is this a pawn Promote event?
            {
                if (flagsReq.IsAny(ChessRequestF.PromoteN))
                    flags |= ChessResultF.PromoteN;       // wanted a Knight.
                else
                    flags |= ChessResultF.PromoteQ;   // Assume a Queen i guess. 
            }

            return flags;
        }

        /// <summary>
        /// Is this move OK for the piece type? and return what will happen as ChessResultF.
        /// Does not change the state of the board.
        /// NOTE: Does not check moves that would put me in check.
        /// </summary>
        /// <param name="piece">ChessPiece</param>
        /// <param name="posNew">ChessPosition</param>
        /// <param name="flagsReq">ChessRequestF</param>
        /// <returns>ChessResultF</returns>
        public ChessResultF TestMove(ChessPiece piece, ChessPosition posNew, ChessRequestF flagsReq)
        {
            if (!piece.IsOnBoard)
                return ChessResultF.Invalid;  // Captured not allowed to move.
            if (!posNew.IsOnBoard)  // can't move off the board.
                return ChessResultF.Invalid; // Blocked;

            ChessType type = piece.Type;
            ChessColor color = piece.Color;
            ChessPosition posTest = piece.Pos;
            int dx = posNew.X - posTest.X;
            int dy = posNew.Y - posTest.Y;
            Debug.Assert(dx != 0 || dy != 0);

            if (piece.IsKing && piece.IsInitPos && !flagsReq.IsAny(ChessRequestF.InCheck) && dy == 0)
            {
                if (dx == ChessOffset.kCastleQ) // Queen side Castle
                {
                    return IsCastleable(color, true) ? ChessResultF.CastleQ : ChessResultF.Invalid;
                }
                else if (dx == ChessOffset.kCastleK) // King side Castle
                {
                    return IsCastleable(color, false) ? ChessResultF.CastleK : ChessResultF.Invalid;
                }
            }

            if (flagsReq.IsAny(ChessRequestF.AssumeValid))        // I already know it is a valid move in general. 
                return TestMove1(piece, posNew, flagsReq);

            bool isOpeningPawn = piece.IsInitPawn && color.GetRank(posNew) == (1 + ChessOffset.kPawnOpen);    // Special pawn opening move.
            int moveSpaces = isOpeningPawn ? ChessOffset.kPawnOpen : type.MoveSpaces;
            ChessOffset[] moveOffsets = type.GetMoveOffsets(color);

            if (moveSpaces == 1)
            {
                if (piece.TypeId != ChessTypeId.Rook)
                {
                    int minSpaces = Math.Min(Math.Abs(dx), Math.Abs(dy));
                    if (minSpaces > 1)
                        return ChessResultF.Invalid;   // too far.
                }

                foreach (var offset in moveOffsets)
                {
                    if (offset.dx != dx || offset.dy != dy)   // exact match?
                        continue;
                    return TestMove1(piece, posNew, flagsReq);
                }
            }
            else
            {
                // limit spaces to MAX of dx,dy
                int maxSpaces = Math.Max(Math.Abs(dx), Math.Abs(dy));
                if (moveSpaces > maxSpaces)
                    moveSpaces = maxSpaces; // limit test distance.

                foreach (var offset in moveOffsets)
                {
                    if (!offset.IsOffsetDir(dx, dy))
                        continue;

                    // Move toward target to see if we can hit it. 
                    posTest = piece.Pos;
                    for (int i = 0; i < moveSpaces; i++)
                    {
                        posTest = posTest.Offset(offset);
                        ChessResultF flags = TestMove1(piece, posTest, flagsReq);
                        if (!flags.IsAllowedMove())
                            return flags;
                        if (posTest.Equals(posNew)) // allowed
                            return flags;
                        if (flags.IsAny(ChessResultF.Capture))
                            return ChessResultF.Invalid;  // Blocked. technically this means I'm blocked before i get there !
                    }

                    // I should never get here !
                    Debug.Assert(false);
                    return ChessResultF.Invalid;   // can be only 1 IsOffsetDir() match in MoveOffsets. so stop looking.
                }
            }

            return ChessResultF.Invalid;
        }

        internal bool IsInDanger(ChessColor color, ChessPosition pos)
        {
            // Given the current board state. Is this position in danger? Attackable by opposite color?

            foreach (ChessPiece piece in Pieces)
            {
                if (piece.Color == color || !piece.IsOnBoard)  // skip colors own pieces.
                    continue;
                if (TestMove(piece, pos, ChessRequestF.Test).IsAllowedMove())  // someone could capture this position!
                    return true;
            }

            return false;
        }
        private bool IsInDanger(ChessPiece king)
        {
            // Given the current board state. Is the King in danger ?
            return IsInDanger(king.Color, king.Pos);
        }

        /// <summary>
        /// Is the King in danger? Given the current board state. 
        /// </summary>
        /// <param name="color">ChessColor</param>
        /// <returns>bool</returns>
        public bool IsInCheck(ChessColor color)
        {
            return IsInDanger(GetPiece(color.KingId));
        }

        /// <summary>
        /// test for ChessResultF.Checkmate assuming I'm in check.
        /// Is there any move they could make that would get out of check?
        /// </summary>
        /// <param name="color">ChessColor</param>
        /// <returns>bool</returns>
        public bool TestCheckmate(ChessColor color)
        {
            foreach (ChessPiece piece in Pieces)
            {
                if (!piece.IsOnBoard || piece.Color == color)
                    continue;
                var moves = GetValidMovesFor(piece, ChessRequestF.Test | ChessRequestF.InCheck);
                if (moves.Count > 0)
                {
                    return false; // some move will get them out of check!
                }
            }
            return true;    // checkmate. // Opposite color Has no possible moves
        }

        /// <summary>
        /// Make a move.
        /// </summary>
        /// <param name="piece"></param>
        /// <param name="posNew"></param>
        /// <param name="flagsReq">ChessRequestF</param>
        /// <param name="testPossible">ChessBestTest</param>
        /// <returns>ChessResultF = result of move. (or failure)</returns>
        public ChessResultF Move(ChessPiece piece, ChessPosition posNew, ChessRequestF flagsReq, ChessBestTester testPossible = null)
        {
            // ChessRequestF.AssumeValid = this is basically valid but test it for check.
            // ChessRequestF.Test = revert this move if successful.

            ChessResultF newFlags = TestMove(piece, posNew, flagsReq);
            if (!newFlags.IsAllowedMove())
            {
                return newFlags;
            }

            ChessColor color = piece.Color;
            newFlags |= color.Flags;
            Debug.Assert(flagsReq.IsAny(ChessRequestF.Test) || color == State.TurnColor);

            int scoreChange = 0;
            ChessPosition posOld = piece.Pos;   // if i have to revert.
            var prevState = new ChessPlayState(State);

            ChessPiece pieceCapture = ChessPiece.kNull;
            if (newFlags.IsAny(ChessResultF.Capture))
            {
                // A capture!
                if (newFlags.IsAny(ChessResultF.EnPassant))
                {
                    pieceCapture = GetAt(posNew.Offset(0, (sbyte)-color.DirY));
                    Grid.ClearAt(pieceCapture.Pos);
                }
                else
                {
                    pieceCapture = GetAt(posNew);
                }

                Debug.Assert(pieceCapture.Color != color);

                switch (pieceCapture.TypeId)
                {
                    case ChessTypeId.King:
                        if (!flagsReq.IsAny(ChessRequestF.Test)) // This should never actually happen.
                        {
                            Debug.Assert(!pieceCapture.IsKing);   // NOT allowed!
                            return ChessResultF.Invalid;
                        }
                        break;
                    case ChessTypeId.Rook: // Color loses ability to castle on that side.
                        var colorCap = pieceCapture.Color;
                        State.GetStateColor(colorCap).CastleFlags |= ChessUtil.GetCastleFlags(colorCap.RookQ == pieceCapture.Id);
                        break;
                }

                SetCaptured(pieceCapture);
                State.MoveLastCapture = State.MoveCount;  // record the move number of the last capture.
                scoreChange = pieceCapture.Value;
            }
            else
            {
                Debug.Assert(Grid.IsEmpty(posNew.X, posNew.Y));     // assert square is empty.
            }

            SetAt2(piece, posNew);

            ChessPieceId rookId = ChessPieceId.QTY;

            if (newFlags.IsAny(ChessResultF.CastleK | ChessResultF.CastleQ))
            {
                // Castle. Move the rook as well!
                Debug.Assert(piece.IsKing);
                bool isQueenSide = newFlags.IsAny(ChessResultF.CastleQ);
                rookId = color.GetRookId(isQueenSide);
                ChessPiece rook = GetPiece(rookId);
                SetAt2(rook, new ChessPosition((byte)(ChessPosition.kXK + (isQueenSide ? -1 : 1)), color.RowKing));
            }

            if (IsInCheck(color)) // I can't move to some place that puts me in check!               
            {
                newFlags |= ChessResultF.CheckBlock;
                flagsReq |= ChessRequestF.Test;  // revert this move.
            }
            else if (!flagsReq.IsAny(ChessRequestF.Test) || testPossible != null)
            {
                // These changes should be recorded before testPossible because they can effect future test moves

                if (newFlags.IsAny(ChessResultF.PromoteQ | ChessResultF.PromoteN))
                {
                    // https://en.wikipedia.org/wiki/Promotion_(chess)
                    ChessTypeId typeIdNew = newFlags.IsAny(ChessResultF.PromoteQ) ? ChessTypeId.Queen : ChessTypeId.Knight;
                    SetPawnType(piece, typeIdNew); // promoted to typeIdNew
                    scoreChange += ChessType.GetType(typeIdNew).Value - ChessType.kPawn.Value;  // from Pawn
                }

                bool isOpeningPawn = false;
                switch (piece.TypeId)
                {
                    case ChessTypeId.Pawn:
                        isOpeningPawn = posOld.Equals(piece.InitPos) && color.GetRank(posNew) == (1 + ChessOffset.kPawnOpen);
                        break;
                    case ChessTypeId.King:
                        State.GetStateColor(color).CastleFlags |= ChessCastleFlags.All;   // move king = can no longer castle.
                        break;
                    case ChessTypeId.Rook:
                        State.GetStateColor(color).CastleFlags |= ChessUtil.GetCastleFlags(color.RookQ == piece.Id); // move rook = can no longer castle on that side.
                        break;
                }

                State.EnPassantPos = isOpeningPawn ? posNew.Offset(0, (sbyte)-color.DirY) : ChessPosition.kNull;    // ONLY This piece is available for EnPassant capture.

                if (IsInCheck(color.Opposite))  // did I put other King in check?
                {
                    newFlags |= ChessResultF.Check;
                    if (TestCheckmate(color))    // is it checkmate?
                    {
                        newFlags |= ChessResultF.Checkmate;   // Has no possible moves
                    }
                }
            }

            if (flagsReq.IsAny(ChessRequestF.Test)) // revert my test move.
            {
                // descend into possible moves and get a score. collect n best.
                testPossible?.TestPossibleNext(piece, newFlags, scoreChange);

                // Revert any changes to board state.
                SetAt1(piece, posOld);
                if (newFlags.IsAny(ChessResultF.EnPassant | ChessResultF.Capture))
                {
                    Debug.Assert(CaptureCount > 0);
                    CaptureCount--;
                    if (newFlags.IsAny(ChessResultF.EnPassant))
                    {
                        SetAt1(pieceCapture, posNew.Offset(0, (sbyte)-color.DirY));
                        Grid.ClearAt(posNew);
                    }
                    else
                    {
                        SetAt1(pieceCapture, posNew);
                    }
                }
                else
                {
                    Grid.ClearAt(posNew);
                }

                if (newFlags.IsAny(ChessResultF.CastleK | ChessResultF.CastleQ))
                {
                    Debug.Assert(piece.IsKing);
                    SetAt2(GetPiece(rookId), ChessPiece.kInitPieces[(byte)rookId].Pos);   // revert castled rook  
                }
                else if (newFlags.IsAny(ChessResultF.PromoteQ | ChessResultF.PromoteN))
                {
                    SetPawnType(piece, ChessTypeId.Pawn);   // revert to pawn. un-promoted.
                }

                State = prevState; // revert any state info.
            }
            else
            {
                // a real move. update score.
                Score += scoreChange * color.ScoreDir;  // update current board score
            }

            return newFlags;
        }

        /// <summary>
        /// what moves can i make with a ChessPiece?
        /// </summary>
        /// <param name="piece"></param>
        /// <param name="flagsReq"></param>
        /// <returns>List all Legal moves for a piece</returns>
        public List<ChessMove> GetValidMovesFor(ChessPiece piece, ChessRequestF flagsReq)
        {
            if (!piece.IsOnBoard)
                return null;  // not allowed to move.

            ChessPosition posOld = piece.Pos;
            ChessColor color = piece.Color;
            ChessType type = piece.Type;

            var possibles = new List<ChessMove>();
            if (piece.IsKing && piece.IsInitPos && !flagsReq.IsAny(ChessRequestF.InCheck))
            {
                if (IsCastleable(color, true)) // Queen side
                {
                    possibles.Add(new ChessMove(posOld.Offset(ChessOffset.kCastleQ, 0), ChessResultF.CastleQ));
                }
                if (IsCastleable(color, false)) // King side
                {
                    possibles.Add(new ChessMove(posOld.Offset(ChessOffset.kCastleK, 0), ChessResultF.CastleK));
                }
            }

            // Get all basically valid (non-check tested) moves
            bool isInitPawn = piece.IsInitPawn;
            var moveOffsets = type.GetMoveOffsets(color);
            foreach (var offset in moveOffsets)
            {
                ChessPosition posNew = posOld;
                byte moveSpaces = type.MoveSpaces;

                if (isInitPawn && offset.dx == 0)
                {
                    moveSpaces = ChessOffset.kPawnOpen; // may make pawn opening move.
                }

                for (int i = 0; i < moveSpaces; i++)
                {
                    posNew = posNew.Offset(offset);
                    ChessResultF flags = TestMove1(piece, posNew, flagsReq);
                    if (!flags.IsAllowedMove())
                        break;
                    possibles.Add(new ChessMove(posNew, flags));
                    if (flags.IsAny(ChessResultF.Capture))   // stop here. blocked from going farther.
                        break;
                }
            }

            // remove all positions that would put/left me in check.
            for (int i = 0; i < possibles.Count; i++)
            {
                ChessPosition posNew = possibles[i].ToPos;
                Debug.Assert(!posOld.Equals(posNew));
                ChessResultF flags = Move(piece, posNew, flagsReq | ChessRequestF.AssumeValid | ChessRequestF.Test);
                if (!flags.IsAllowedMove()) // Make a test move then revert
                {
                    possibles.RemoveAt(i);    // was not a valid move. (would have put me in check)
                    i--;
                }
            }

            return possibles;
        }

        //*******************

        public ChessGameBoard()
            : base()
        {
            // Score = 0;
        }

        public ChessGameBoard(IEnumerable<ChessPiece> pieces) : base(pieces)
        {
            State.White.CastleFlags |= GetCastleFlagsMin(ChessColor.kWhite);
            State.Black.CastleFlags |= GetCastleFlagsMin(ChessColor.kBlack); // Must have at least this.
            Score = GetScore();
        }

        public ChessGameBoard(string stateString) : base(stateString)
        {
            Score = GetScore();
        }

        public ChessGameBoard(ChessGameBoard clone) : base(clone)
        {
            Score = clone.Score;
        }

        public ChessGameBoard(string[] fen, int i = 0, bool hasOrder = true) 
            : base(fen, i, hasOrder)
        {
            State.White.CastleFlags |= GetCastleFlagsMin(ChessColor.kWhite);
            State.Black.CastleFlags |= GetCastleFlagsMin(ChessColor.kBlack); // Must have at least this.
            Score = GetScore();
        }
    }
}
