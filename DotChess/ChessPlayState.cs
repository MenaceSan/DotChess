//  
// Copyright (c) 2020 Dennis Robinson (www.menasoft.com). All rights reserved.  
// Licensed under the MIT License. See ReadMe.md file in the project root for full license information.  
// 
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace DotChess
{
    /// <summary>
    /// ChessPlayState color/player specific state information
    /// </summary>
    public class ChessStateColor
    {
        public ChessCastleFlags CastleFlags;     // Have we moved a rook or king to prevent future castling.
        public TimeSpan Time;   // How much time waiting for Player in this game? Auto resign after TimeMax
 
        public ChessStateColor(ChessCastleFlags flags = 0)
        {
            CastleFlags = flags;
        }        
        public ChessStateColor(ChessStateColor clone)
        {
            CastleFlags = clone.CastleFlags;
            Time = clone.Time;
        }
    }

    /// <summary>
    /// Current play state for the game board. Params used for FEN.
    /// NOTE: InCheck is not stored here since it can be better derived from the board.
    /// </summary>
    public class ChessPlayState
    {
        public const int kMovesMax = 999;

        public int MoveCount;  // Whose turn is it to move now ? completed moves. even = white. e.g. 1 = waiting for black to move. Not the same as TurnNumber. AKA Half Moves, Ply/Plies
        public ChessPosition EnPassantPos;        // If last move has EnPassant potential. This is the position 'behind' the pawn. IsOpeningPawn else ChessPosition.kNull
        public readonly ChessStateColor White;
        public readonly ChessStateColor Black;
        public int MoveLastCapture;      // When was the last capture? for 50 moves count. Stalemate.

        public DateTime LastMove;   // when did last move end and current move start. waiting for TurnColor to move.

        public bool IsWhiteTurn => ChessUtil.IsEven(MoveCount);  // Whose turn is it to move next/now? White moves first. 1 based.
        public ChessColor TurnColor => GetColorForMove(MoveCount);  // Whose turn is it to move next/now? White moves first. 1 based.
        public int TurnNumber => GetTurnNumberForMove(MoveCount);     // 1 base count for both sides taking a turn. 
        public int MovesSinceCapture => MoveCount - MoveLastCapture;

        public bool IsStalemate => MovesSinceCapture >= 50;     // the 50 move rule ? Stalemate

        /// <summary>
        /// Get the ChessStateColor that corresponds to ChessColor
        /// </summary>
        /// <param name="color">ChessColor</param>
        /// <returns>ChessStateColor</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ChessStateColor GetStateColor(ChessColor color)
        {
            return color.IsWhite ? White : Black;
        }

        /// <summary>
        /// Get turn number = starts over each time white plays.
        /// </summary>
        /// <param name="moveCount">0 based</param>
        /// <returns>Turn number 1 based</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetTurnNumberForMove(int moveCount)
        {
            return (moveCount / 2) + 1;
        }

        /// <summary>
        /// What color should move now?
        /// </summary>
        /// <param name="moveCount">0 based.</param>
        /// <returns>ChessColor</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChessColor GetColorForMove(int moveCount)
        {
            return ChessUtil.IsEven(moveCount) ? ChessColor.kWhite : ChessColor.kBlack;
        }

        internal void MoveAdvance(ChessColor color)
        {
            // Active Game. The current move is complete. Advance Turn.

            var now = DateTime.UtcNow;
            if (LastMove != DateTime.MinValue)
            {
                GetStateColor(color).Time += (now - LastMove);
            }

            LastMove = now;
            MoveCount++;
        }

        /// <summary>
        /// get game state as hash code. NOT move count, just state of the board.
        /// NOTE: ANY changes here invalidates the db hashes.
        /// </summary>
        /// <param name="hashedValue">ulong</param>
        /// <param name="transpose">Act as though black was white.</param>
        /// <returns></returns>
        public ulong GetHashCode64(ulong hashedValue, bool transpose)
        {
            if (transpose)
            {
                hashedValue += EnPassantPos.IsOnBoard ? EnPassantPos.GetTransposed().HashCode64 : 0;
                hashedValue *= ChessUtil.kHashValue2;
                hashedValue += (uint)Black.CastleFlags;
                hashedValue *= ChessUtil.kHashValue2;
                hashedValue += (uint)White.CastleFlags;
            }
            else
            {
                hashedValue += EnPassantPos.IsOnBoard ? EnPassantPos.HashCode64 : 0;
                hashedValue *= ChessUtil.kHashValue2;
                hashedValue += (uint)White.CastleFlags;
                hashedValue *= ChessUtil.kHashValue2;
                hashedValue += (uint)Black.CastleFlags;
            }

            hashedValue *= ChessUtil.kHashValue2;

            return hashedValue;
        }

        public void GetStateString(StringBuilder sb)
        {
            // Get simpler/shorter (restore-able) state string. 

            Debug.Assert(MoveCount <= kMovesMax);

            sb.Append(ChessUtil.ToChar(MoveCount / 100));
            sb.Append(ChessUtil.ToChar((MoveCount / 10) % 10));
            sb.Append(ChessUtil.ToChar(MoveCount % 10));
            sb.Append(EnPassantPos.Notation);   // 2
            int movesSinceCapture = MovesSinceCapture;
            sb.Append(ChessUtil.ToChar(movesSinceCapture / 10));
            sb.Append(ChessUtil.ToChar(movesSinceCapture % 10));
            sb.Append(ChessUtil.GetCastleAvailChar(White.CastleFlags));
            sb.Append(ChessUtil.GetCastleAvailChar(Black.CastleFlags));
        }

        public void GetFEN(StringBuilder sb)
        {
            // Get 5 sections of the FEN state for the game.

            sb.Append(TurnColor.FenLetter); // b/w
            sb.Append(ChessUtil.kFenSep);

            int len = sb.Length;
            if (!White.CastleFlags.IsAny(ChessCastleFlags.K))
                sb.Append('K');
            if (!White.CastleFlags.IsAny(ChessCastleFlags.Q))
                sb.Append('Q');
            if (!Black.CastleFlags.IsAny(ChessCastleFlags.K))
                sb.Append('k');
            if (!Black.CastleFlags.IsAny(ChessCastleFlags.Q))
                sb.Append('q');
            if (len == sb.Length)   // nothing?
                sb.Append('-');
            sb.Append(ChessUtil.kFenSep);

            if (EnPassantPos.IsOnBoard)
            {
                sb.Append(EnPassantPos.Notation);
            }
            else
            {
                sb.Append('-');
            }
            sb.Append(ChessUtil.kFenSep);

            sb.Append(MovesSinceCapture);
            sb.Append(ChessUtil.kFenSep);

            sb.Append(TurnNumber);
        }

        public ChessPlayState(string stateString, int i)
        {
            // Parse parts of GetStateString.
            if (stateString.Length - i < 9)
            {
                ChessGame.InternalFailure("ChessPlayState state");
                return;
            }

            MoveCount = ChessUtil.FromChar(stateString[i + 0]) * 100 + ChessUtil.FromChar(stateString[i + 1]) * 10 + ChessUtil.FromChar(stateString[i + 2]);
            EnPassantPos = new ChessPosition(stateString[i + 3], stateString[i + 4]);

            int movesSinceCapture = ChessUtil.FromChar(stateString[i + 5]) * 10 + ChessUtil.FromChar(stateString[i + 6]);
            MoveLastCapture = MoveCount - movesSinceCapture;

            White = new ChessStateColor(ChessUtil.GetCastleFlags(stateString[i + 7])); 
            Black = new ChessStateColor(ChessUtil.GetCastleFlags(stateString[i + 8]));
        }

        public const int kFenParams = 5;

        public ChessPlayState(string[] fen, int i)
        {
            // Parse parts of the FEN string. 5 parts. kFenParams

            bool isWhite = (fen[i + 0][0] == 'w');  // [0]
            ChessColor color = isWhite ? ChessColor.kWhite : ChessColor.kBlack;

            White = new ChessStateColor(ChessCastleFlags.All); // Assume no castling allowed unless explicitly allowed.
            Black = new ChessStateColor(ChessCastleFlags.All);
 
            string castleAvail = fen[i + 1];    // Castle avail flags. // [1]
            foreach (char ch in castleAvail)
            {
                switch (ch) // allowed?
                {
                    case 'K':
                        White.CastleFlags &= ~ChessCastleFlags.K;
                        break;
                    case 'Q':
                        White.CastleFlags &= ~ChessCastleFlags.Q;
                        break;
                    case 'k':
                        Black.CastleFlags &= ~ChessCastleFlags.K;
                        break;
                    case 'q':
                        Black.CastleFlags &= ~ChessCastleFlags.Q;
                        break;
                }
            }

            if (i + 2 >= fen.Length)
                return;
            string enpassantPos = fen[i + 2];    // [2]
            if (ChessPosition.IsValidNotation(enpassantPos, 0))
            {
                EnPassantPos = new ChessPosition(enpassantPos[0], enpassantPos[1]);
            }
            else
            {
                if (enpassantPos[0] != '-')
                {
                    ChessGame.InternalFailure("ChessPlayState FEN");
                }
                EnPassantPos = ChessPosition.kNull;
            }

            if (i + 3 >= fen.Length)
                return;
            if (int.TryParse(fen[i + 3], out int movesSinceCapture))        // [3]
            {
            }

            if (i + 4 >= fen.Length)
                return;
            if (int.TryParse(fen[i + 4], out int turns))    // [4]
            {
                MoveCount = (turns - 1) * 2;
            }
            if (!isWhite)
                MoveCount++;

            MoveLastCapture = MoveCount - movesSinceCapture;
        }

        public ChessPlayState(ChessPlayState clone)
        {   
            // clone.
            MoveCount = clone.MoveCount;
            MoveLastCapture = clone.MoveLastCapture;
            EnPassantPos = clone.EnPassantPos;
            White = new ChessStateColor(clone.White);
            Black = new ChessStateColor(clone.Black);
            LastMove = clone.LastMove;
        }
        public ChessPlayState()
        {
            EnPassantPos = ChessPosition.kNull;
            White = new ChessStateColor();
            Black = new ChessStateColor();
            LastMove = DateTime.UtcNow;  // when?
        }
    }
}
