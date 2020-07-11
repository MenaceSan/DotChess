//  
// Copyright (c) 2020 Dennis Robinson (www.menasoft.com). All rights reserved.  
// Licensed under the MIT License. See ReadMe.md file in the project root for full license information.  
// 
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DotChess
{
    /// <summary>
    /// Chess game play mode.
    /// </summary>
    public enum ChessModeId : byte
    {
        NoRules = 0,   // Anyone can just move stuff around. (Not in tournament mode)
        PvP,
        PvC,    // Black is AI (computer)
        CvP,    // White is AI (computer)
        CvC,
        QTY,
    }

    /// <summary>
    /// The sides/team/color to the chess game board. Who wins?
    /// </summary>
    public enum ChessColorId : byte
    {
        Undefined = 0, // kActive. No winner yet. '*'. Game still in play.
        White = 1,
        Black = 2,
        Stalemate = 3,  // No winning move is available or the 50 move rule broken.
    }

    /// <summary>
    /// A chess piece type that has particular moves.
    /// </summary>
    public enum ChessTypeId : byte
    {
        Rook = 0,   // R, 
        Knight,     // N, 
        Bishop,     // B, 
        Queen,      // Q, 
        King,       // K, 
        Pawn,       // blank/space/nothing or may use the row (a-h) or P
        QTY,
    }

    /// <summary>
    /// A piece on the chess board. Board has Max 32 pieces.
    /// Piece Type is constant to start but pawns may be Promoted. ChessPieceType
    /// </summary>
    [Serializable]
    public enum ChessPieceId : byte
    {
        // White pieces. Move First.

        WQR = 0,    // a1 // Rook
        WQN,    // b1 // Knight
        WQB,    // c1 // Bishop
        WQ,     // Queen
        WK,     // King (+)
        WKB,
        WKN,
        WKR = 7,

        WPa,    // a2
        WPb,    // b2
        WPc,
        WPd,
        WPe,
        WPf,
        WPg,
        WPh = 15,

        QTY1 = 16,

        // Black pieces.

        BQR = 16,    // a8
        BQN,    // b8
        BQB,    // c8
        BQ,
        BK,     // NOT actually correct board order ? ok.
        BKB,
        BKN,
        BKR = 23,

        BPa,    // a7
        BPb,    // b7
        BPc,
        BPd,
        BPe,
        BPf,
        BPg,
        BPh,

        QTY = 32,
    }

    /// <summary>
    /// Move request options.
    /// </summary>
    [Flags]
    public enum ChessRequestF : byte
    {
        Test = 0x01,          // This is a test move. Revert it when done.
        AssumeValid = 0x02,   // Assume the move is basically valid. Testing for Check.
        InCheck = 0x04,       // We are currently in check so we can't castle.
        PromoteN = 0x08,      // Move will promote pawn to knight else queen.
    }

    /// <summary>
    /// Result of a move or a test for move.
    /// </summary>
    [Serializable]
    [Flags]
    public enum ChessResultF : ushort
    {
        OK = 0,                 // Move is OK.
        Capture = 0x0001,       // Move will captured another piece. 'x'

        Check = 0x0002,         // Results in check on opposite colors king. '+'
        PromoteN = 0x0004,      // Move will Promote pawn. (to knight)  https://en.wikipedia.org/wiki/Promotion_(chess)
        PromoteQ = 0x0008,      // Move will Promote pawn. (to queen default)  https://en.wikipedia.org/wiki/Promotion_(chess)
        CastleK = 0x0010,       // O-O  = kings side castle. (short)
        CastleQ = 0x0020,       // O-O-O = queen side castle. (long)

        Checkmate = 0x0040,     // Game is over.
        Stalemate = 0x0080,     // Game is Stalemate (Draw). 50 moves without capture or XX repeats. No legal moves but not in check? https://en.wikipedia.org/wiki/Stalemate
        Resigned = 0x0100,      // Last player resigned. Game is over. Was probably check mated? or would be.

        Good = 0x0200,          // ! Marked as an good/excellent move in the notes. 
        Bad = 0x0400,           // ? Marked as an bad/questionable move in the notes. don't use it.

        EnPassant = 0x0800,     // an EnPassant Capture of a pawn by another pawn. https://en.wikipedia.org/wiki/En_passant 
        Invalid = 0x1000,       // Invalid move. Not a valid move for this type of piece. block by own piece or out of bounds. piece is captured and off the board
        CheckBlock = 0x2000,    // Move is blocked because it results in check on me. (or doesnt clear an existing check)

        ColorW = 0x4000,        // Turn is/was White. // Can be used with ChessResultF.Resigned.  
        ColorB = 0x8000,        // Turn is/was Black. Combine to support 4 player mode? NOT Needed ??
    }

    /// <summary>
    /// Flag for castling (UN)availability. Similar to FEN notation.
    /// Record that castle has been used and/or cant be used (again) because a piece moved.
    /// </summary>
    [Flags]
    public enum ChessCastleFlags : byte
    {
        K = 0x01,    // White kings side castle used. O-O  (short) 
        Q = 0x02,    // White queen side castle used. O-O-O (long)
        All = 0x03,     // all flags. No Castle is available.
    }

    /// <summary>
    /// Catch all for useful methods and extensions.
    /// </summary>
    public static class ChessUtil
    {
        public const char kFenSep = ' ';

        public const ulong kHashValue1 = 3074457345618258791ul; // start value. GetKnuthHash
        public const ulong kHashValue2 = 3074457345618258799ul;

        public const int kThreadsMax = 32;     // MAX number of Threads to run in parallel? 1 16 32 . Set to 1 for DEBUG.

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAny(this ChessResultF flags, ChessResultF flagsTest)
        {
            // does flags have any of the flagsTest bits set?
            return (flags & flagsTest) != 0;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAllowedMove(this ChessResultF flags)
        {
            return !flags.IsAny(ChessResultF.Invalid | ChessResultF.CheckBlock);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsComplete(this ChessResultF flags) // Game is over?
        {
            return flags.IsAny(ChessResultF.Checkmate | ChessResultF.Resigned | ChessResultF.Stalemate);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAny(this ChessRequestF flags, ChessRequestF flagsTest)
        {
            // does flags have any of the flagsTest bits set?
            return (flags & flagsTest) != 0;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChessRequestF GetReqInCheck(this ChessResultF flags)
        {
            return flags.IsAny(ChessResultF.Check) ? ChessRequestF.InCheck : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsEven(int n)
        {
            return (n & 1) == 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static char ToChar(int i)
        {
            return (char)(i + '0');
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int FromChar(char ch)
        {
            return (int)(ch - '0');
        }

        public const string kCastleAvailChar = "*QK-";      // Castle availability/use notation. 0=both, 1=queen, 2=king, 3=none. White = upper case.
        public static char GetCastleAvailChar(ChessCastleFlags flags)
        {
            // Get a GetStateString character for ChessCastleFlags for color.
            return kCastleAvailChar[(int)flags];
        }
        public static ChessCastleFlags GetCastleFlags(char ch)
        {
            // Parse GetCastleAvailChar() back to ChessCastleFlags for parse of GetStateString()
            int i = kCastleAvailChar.IndexOf(ch);
            if (i < 0)
                return 0;
            return (ChessCastleFlags)i;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAny(this ChessCastleFlags flags, ChessCastleFlags flagsTest)
        {
            return (flags & flagsTest) != 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChessCastleFlags GetCastleFlags(bool isQueenSide)
        {
            return isQueenSide ? ChessCastleFlags.Q : ChessCastleFlags.K;
        }
    }

    /// <summary>
    /// A delta position move for a type of piece.
    /// </summary>
    public struct ChessOffset
    {
        public readonly sbyte dx;
        public readonly sbyte dy;
        public readonly bool isDiagonal;

        public const sbyte kCastleK = 2;     // Castle will X offset the king this many spaces.
        public const sbyte kCastleQ = -2;     // Castle will X offset the king this many spaces.

        public const byte kPawnOpen = 2;    // Y offset in color direction. makes the pawn EnPassant available.

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsOffsetSign(sbyte offset1, int dv)
        {
            // does the single dimension offset match direction?
            switch (offset1)
            {
                case 0: return dv == 0;
                case 1: return dv > 0;
                case -1: return dv < 0;
            }
            Debug.Assert(false);  // should NEVER happen. Rook (-2) will never come here.
            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsOffsetDir(int _dx, int _dy)
        {
            // does the offset match direction? vector 2.
            if (!IsOffsetSign(this.dx, _dx) || !IsOffsetSign(this.dy, _dy))
                return false;
            if (isDiagonal)
                return Math.Abs(_dx) == Math.Abs(_dy); // Must be an exact diagonal.
            return true;
        }

        public ChessOffset(sbyte _dx, sbyte _dy) { dx = _dx; dy = _dy; isDiagonal = Math.Abs(_dx) == Math.Abs(_dy); }
    }

    /// <summary>
    /// A position on the board or in capture area.
    /// 'a1' = lower left of screen board from whites point of view.
    /// queen is 'D' file, on left side of King.
    /// white = bottom row (1) 
    /// </summary>
    [Serializable]
    public struct ChessPosition : IEquatable<ChessPosition>
    {
        public readonly byte X;    // 0-7 -> a-h = x/column/file.
        public readonly byte Y;    // 0-7 -> 1-8 = y/row/rank

        public const byte kDim = 8;    // board is a matrix of 8x8 (a-h)x(1-8)
        public const byte kDim1 = kDim - 1;     // RowKing or RowPromote.
        public const byte kDim2 = kDim - 2;     // RowPawn
        public const byte kDim3 = kDim - 3;     // EnPassant

        public const char kX0 = 'a';    // file.
        public const char kY0 = '1';    // rank.

        public const byte kXK = 4;      // King x position.

        public const byte kNullVal = (byte)ChessPieceId.QTY;  // NOT a valid value for anything.
        public static readonly ChessPosition kNull = new ChessPosition(kNullVal, kNullVal);   // a not valid value. !IsOnBoard and ! IsValidCaptured

        public const char kDimCharX = (char)(kX0 + kDim); // 'i' = GetLetterX(kDim) // indicates capture.

        public bool IsOnBoardX => X < kDim;
        public bool IsOnBoardY => Y < kDim;
        public bool IsOnBoard => IsOnBoardX && IsOnBoardY;    // notation for a piece not currently on the board. Invalid position. IsCaptured
        public bool IsValidCaptured => X == kDim && Y < kNullVal;
        public bool IsSquareWhite => !ChessUtil.IsEven(X ^ Y);    // on a white board square? used for testing bishops. only valid if IsOnBoard
        public ushort BitIdx => GetBitIdx(X,Y);                 // unique code that does NOT include captured positions.
        public uint HashCode64 => ((uint)Y * kNullVal) + X;     // unique code that will include captured positions.

        /// <summary>
        /// Get value from 0 to 63.
        /// ASSUME IsOnBoard. Place on Grid or in 64 bit mask.
        /// </summary>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns>unique code that does NOT include captured positions.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ushort GetBitIdx(byte x, byte y)
        {
            Debug.Assert(x < kDim && y < kDim);
            return (ushort)((y * kDim) + x);
        }

        public ChessPosition GetTransposed()
        {
            // Get equiv opposite color position for transposed Black/White
            Debug.Assert(IsOnBoard);
            return new ChessPosition((byte)(kDim1 - X), (byte)(kDim1 - Y));
        }

        public bool IsValidBishop(bool isSquareWhite)
        {
            // is this position valid for a bishop?
            Debug.Assert(IsValidCaptured || IsOnBoard);
            return !IsOnBoard || isSquareWhite == IsSquareWhite;
        }

        public static bool IsXFile(char cx)
        {
            // Is valid Notation letter? IsOnBoard
            return cx >= kX0 && cx <= 'h';
        }
        public static bool IsYRank(char cy)
        {
            // Is valid Notation char?  IsOnBoard
            return cy >= kY0 && cy <= '8';
        }

        public static char GetLetterX(byte x)
        {
            // as char Letter
            Debug.Assert(x <= kNullVal);
            return (char)(x < 26 ? (x + kX0) : ((x - 26) + '0'));
        }
        public static byte GetX(char cx)
        {
            // as char Letter. reverse of GetLetterX()
            if (cx >= kX0 && cx <= 'z')
                return (byte)(cx - kX0);
            if (cx >= '0' && cx <= '9')
                return (byte)((cx - '0') + 26);
            ChessGame.InternalFailure("GetX");
            return 0;   // BAD
        }

        public static char GetCharY(byte y)
        {
            // as char Number
            Debug.Assert(y <= kNullVal);
            return (char)(y < 9 ? (y + kY0) : ((y - 9) + 'a'));
        }
        public static byte GetY(char cy)
        {
            // as char Number. reverse of GetCharY()
            if (cy >= kY0 && cy <= '9')
                return (byte)(cy - kY0);
            if (cy >= 'a' && cy <= 'z')
                return (byte)((cy - 'a') + 9);
            ChessGame.InternalFailure("GetY");
            return 0;   // BAD
        }

        public byte GetCaptureCount()
        {
            // 0 based value for the capture order.
            Debug.Assert(IsValidCaptured || Y == kNullVal);
            return Y;
        }

        public char NotationX => GetLetterX(X);
        public char NotationY => GetCharY(Y);

        public string Notation  // Where is this piece now. e.g. "a3"
        {
            get
            {
                if (IsOnBoard)
                {
                    return string.Concat(NotationX, NotationY);
                }
               
                // non standard notation for capture count. "ix" = kNull.
                return string.Concat(kDimCharX, GetCharY(GetCaptureCount()));
            }
        }
        public new string ToString()
        {
            return Notation;
        }

        public static byte Offset1(byte v, sbyte o)
        {
            // Get position + offset.
            return (byte)(v + o);
        }
        public ChessPosition Offset(sbyte dx, sbyte dy)
        {
            // Get position + offset.
            return new ChessPosition(Offset1(X, dx), Offset1(Y, dy));
        }
        public ChessPosition Offset(ChessOffset d)
        {
            // Get position + offset.
            return Offset(d.dx, d.dy);
        }

        public bool Equals(ChessPosition other)
        {
            // IEquatable
            return X == other.X && Y == other.Y;
        }
        public static int Compare2(ChessPosition x, ChessPosition y)
        {
            // For sorting in a list. like IComparable<ChessPosition>
            int diff = x.X - y.X;
            if (diff != 0)
                return diff;
            return x.Y - y.Y;
        }

        public static bool IsValidNotation(string notation, int i)
        {
            // Is this string a valid IsOnBoard position in notation format? NOT Captured.
            return notation.Length >= i + 2 && IsXFile(notation[i + 0]) && IsYRank(notation[i + 1]);
        }
 
        public ChessPosition(byte captureCount)
        {
            // create a piece no longer on the board. IsCaptured
            X = kDim;
            Y = captureCount;     // order of capture. Starting at 0.
            Debug.Assert(IsValidCaptured);
        }

        public ChessPosition(byte x, byte y)
        {
            // create a position on the board. May not be valid. May be captured?  
            X = x;
            Y = y;
        }
        public ChessPosition(char cxFile, char cyRank)
        {
            // create a position on the board. 
            X = GetX(cxFile);
            Y = GetY(cyRank);
            Debug.Assert(IsOnBoard || IsValidCaptured || Y == kNullVal);
        }
    }

    /// <summary>
    /// Define game/board features that are color/side/team specific.
    /// </summary>
    public class ChessColor
    {
        public readonly ChessColorId Id;
        public readonly bool IsWhite;
        public readonly char FenLetter;     // b,w

        public readonly int ScoreDir;   // How does this change the score ? +1 or -1.
        public readonly sbyte DirY;               // What Y direction do pawns move? +1 or -1
        public readonly byte RowKing; // Get the kings rank/row for this color. 0
        public readonly ChessResultF Flags;       // ChessResultF.ColorW, ChessResultF.ColorB can be used with ChessFlags.Resigned
        public readonly ChessOffset[] PawnMoves;    // Pawn moves specific to this color for ChessType.
        public readonly string ResultWin;      // I win. ChessNotation1.kWinBlack : ChessNotation1.kWinWhite

        public readonly ChessPieceId KingId;
        public readonly ChessPieceId RookQ;
        public readonly ChessPieceId RookK;

        public ChessColor Opposite => IsWhite ? kBlack : kWhite;  // get Opposite color.

        static readonly ChessOffset[] _MovesPawnW = new ChessOffset[]       // MUST be defined BEFORE kWhite, kBlack for static init order to work !!!!
        {
            new ChessOffset(-1,1),
            new ChessOffset(0,1),
            new ChessOffset(1,1),
        };
        static readonly ChessOffset[] _MovesPawnB = new ChessOffset[]
        {
            new ChessOffset(-1,-1),
            new ChessOffset(0,-1),
            new ChessOffset(1,-1),
        };

        public static readonly ChessColor kWhite = new ChessColor(ChessColorId.White);
        public static readonly ChessColor kBlack = new ChessColor(ChessColorId.Black);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChessColor GetColor(ChessColorId id)
        {
            switch (id)
            {
                case ChessColorId.White:
                    return kWhite;
                case ChessColorId.Black:
                    return kBlack;
                default:
                    return null;
            }
        }

        public new string ToString()
        {
            return Id.ToString();
        }

        public static ChessColor GetColorFromChar(char ch)
        {
            // Upper case = White. FEN
            return char.IsUpper(ch) ? kWhite : kBlack;
        }
        public static char ToFenTranspose(char ch)
        {
            // Transpose case. swapping black/white FEN char.
            if (char.IsUpper(ch))
                return char.ToLower(ch);
            return char.ToUpper(ch);
        }

        public char GetFENChar(char ch)
        {
            // Change case of type letter for color. White = upper case, black = lower case. For FEN coding.
            // ASSUME ch = Upper case
            Debug.Assert(char.IsUpper(ch) || !char.IsLetter(ch));
            return IsWhite ? ch : char.ToLower(ch);
        }

        public ChessPieceId GetRookId(bool isQueenSide)
        {
            // Get Queen/King side rook for color           
            return isQueenSide ? RookQ : RookK;
        }

        public byte GetRank(ChessPosition pos)
        {
            // Get rank (Y) from the perspective of the color.
            if (IsWhite)
                return pos.Y;
            return (byte)(ChessPosition.kDim1 - pos.Y);
        }

        public ChessColor(ChessColorId colorId)
        {
            Id = colorId;
            IsWhite = colorId == ChessColorId.White;
            FenLetter = IsWhite ? 'w' : 'b';

            ScoreDir = IsWhite ? 1 : -1;
            DirY = (sbyte)(IsWhite ? 1 : -1);
            RowKing = (byte)(IsWhite ? 0 : ChessPosition.kDim1); // Get the kings rank/row for this color. 0
            Flags = IsWhite ? ChessResultF.ColorW : ChessResultF.ColorB;
            PawnMoves = IsWhite ? _MovesPawnW : _MovesPawnB;
            Debug.Assert(PawnMoves != null);    // static init order must be correct.
            ResultWin = IsWhite ? ChessNotation1.kWinWhite : ChessNotation1.kWinBlack;

            KingId = IsWhite ? ChessPieceId.WK : ChessPieceId.BK;
            RookQ = IsWhite ? ChessPieceId.WQR : ChessPieceId.BQR;
            RookK = IsWhite ? ChessPieceId.WKR : ChessPieceId.BKR;
        }
    }

    /// <summary>
    /// What type of chess piece is this? What moves can a piece type make?
    /// </summary>
    public class ChessType
    {
        public readonly ChessTypeId Id;
        private readonly ChessOffset[] MoveOffsets;   // What direction can this piece move?
        public readonly byte MoveSpaces;  // how far can this piece move unobstructed? exception -> Rook = 1.
        public readonly int Value;      // What is this piece type worth ? 30 + king * kValueBase // https://www.chess.com/article/view/chess-piece-value

        public const int kValueTick = 1;        // tiny effect.
        public const int kValuePawn = 1000;
        public const int kValueKing = 32 * kValuePawn;

        public const string kTypeChars = "RNBQKP"; // P is sometimes just omitted. black pieces can use lowercase for FEN.
        public readonly char TypeChar;      // from kTypeChars

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ChessOffset[] GetMoveOffsets(ChessColor color)
        {
            // How might each type move?
            if (Id == ChessTypeId.Pawn)    // pawns are special
            {
                return color.PawnMoves;
            }
            return MoveOffsets;
        }

        public static int GetTypeIdFrom(char ch)
        {
            // Get ChessTypeId from a char. kTypeChars. ASSUME upper case.
            // -1 = not here. else ChessTypeId
            return kTypeChars.IndexOf(ch);  // may return -1;
        }

        internal char GetFEN(ChessColor color)
        {
            // get TypeChar letter with proper case for ChessColor. For FEN coding.
            return color.GetFENChar(TypeChar);
        }

        static readonly ChessOffset[] _MovesKing = new ChessOffset[]
        {
            new ChessOffset(1,1),
            new ChessOffset(1,0),
            new ChessOffset(1,-1),
            new ChessOffset(0,-1),
            new ChessOffset(-1,-1),
            new ChessOffset(-1,0),
            new ChessOffset(-1,1),
            new ChessOffset(0,1),
        };
        static readonly ChessOffset[] _MovesRook = new ChessOffset[]
        {
            new ChessOffset(1,0),
            new ChessOffset(0,-1),
            new ChessOffset(-1,0),
            new ChessOffset(0,1),
        };
        static readonly ChessOffset[] _MovesBishop = new ChessOffset[]
        {
            new ChessOffset(1,1),
            new ChessOffset(1,-1),
            new ChessOffset(-1,-1),
            new ChessOffset(-1,1),
        };
        static readonly ChessOffset[] _MovesKnight = new ChessOffset[]
        {
            new ChessOffset(-1,2),
            new ChessOffset(1,2),
            new ChessOffset(2,1),
            new ChessOffset(2,-1),
            new ChessOffset(1,-2),
            new ChessOffset(-1,-2),
            new ChessOffset(-2,1),
            new ChessOffset(-2,-1),
        };

        public readonly static ChessType kKnight = new ChessType(ChessTypeId.Knight, _MovesKnight, 1, 3 * kValuePawn); // Knight has 8 possible places to go.
        public readonly static ChessType kQueen = new ChessType(ChessTypeId.Queen, _MovesKing, ChessPosition.kDim, 9 * kValuePawn);
        public readonly static ChessType kPawn = new ChessType(ChessTypeId.Pawn, null, 1, 1 * kValuePawn); // 1*8 = 8

        private readonly static ChessType[] kTypes = new ChessType[]
        {
            new ChessType( ChessTypeId.Rook, _MovesRook, ChessPosition.kDim, 5 * kValuePawn ),
            kKnight,
            new ChessType( ChessTypeId.Bishop, _MovesBishop, ChessPosition.kDim, 3 * kValuePawn ),
            kQueen,
            new ChessType( ChessTypeId.King, _MovesKing, 1, kValueKing ),
            kPawn,
        };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChessType GetType(ChessTypeId typeId)
        {
            return kTypes[(byte)typeId];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsValidId(ChessTypeId typeId)
        {
            return typeId < ChessTypeId.QTY;
        }

        public ChessType(ChessTypeId id, ChessOffset[] moveOffsets, byte moveSpaces, int value)
        {
            Id = id;
            MoveOffsets = moveOffsets;   // What direction can this piece move?
            MoveSpaces = moveSpaces;  // how far can this piece move unobstructed?
            Value = value;
            TypeChar = kTypeChars[(int)Id];
        }
    }
}
