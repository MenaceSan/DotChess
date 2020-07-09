//  
// Copyright (c) 2020 Dennis Robinson (www.menasoft.com). All rights reserved.  
// Licensed under the MIT License. See ReadMe.md file in the project root for full license information.  
// 
#define USE_StructPiece // faster.

using System.Runtime.CompilerServices;

namespace DotChess
{
    /// <summary>
    /// Information about a piece on the ChessBoard. (4 bytes of information) 
    /// </summary>
    public struct ChessPiece
    {
        public readonly byte IdIdx;         // ChessPieceId as byte.
        public ChessTypeId TypeId;          // What type am I? Pawns may be Promoted. (byte)
        public ChessPosition Pos;           // My current position on board. test IsCaptured. (2 bytes)

        public ChessPieceId Id => (ChessPieceId)IdIdx;    // What piece am I?
        public ChessColor Color => IsWhite(Id) ? ChessColor.kWhite : ChessColor.kBlack;        // what color/side ?  

        public ChessPiece Init => kInitPieces[IdIdx];
        public ChessPosition InitPos => Init.Pos;
        public bool IsInitPos => Pos.Equals(InitPos); // in its starting position?
        public bool IsOnBoard => Pos.IsOnBoard;
        public bool IsKing => TypeId == ChessTypeId.King;
        public bool IsPawnId => Init.TypeId == ChessTypeId.Pawn;  // Is/was a pawn but might have been promoted?
        public bool IsPawnType => TypeId == ChessTypeId.Pawn; // A pawn that may be promoted in the future.

        public ChessType Type => ChessType.GetType(TypeId);
        public int Value => Type.Value;
        public char FEN => this.Type.GetFEN(this.Color);

        public bool IsInitPawn => TypeId == ChessTypeId.Pawn && IsInitPos;  // is this a pawn in its starting position?

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsWhite(ChessPieceId id)
        {
            return id <= ChessPieceId.WPh;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsValidId(ChessPieceId id)
        {
            return id < ChessPieceId.QTY;
        }

        public static ChessPieceId GetTransposed(ChessPieceId id)
        {
            // Transpose a black ChessPieceId to/from a white ChessPieceId.
            if (IsWhite(id))
                return id + (int)ChessPieceId.QTY1;
            return id - (int)ChessPieceId.QTY1;
        }

        public new string ToString()
        {
            return Id.ToString();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsMatch(ChessTypeId typeId, ChessColor color)
        {
            // FEN notation would see this as the same piece.
            return TypeId == typeId && Color == color;
        }

        internal static readonly ChessPiece[] kInitPieces = new ChessPiece[] // All pieces on both sides.
        {
            new ChessPiece(ChessPieceId.WQR, ChessTypeId.Rook,   new ChessPosition(0,0)),
            new ChessPiece(ChessPieceId.WQN, ChessTypeId.Knight, new ChessPosition(1,0)),
            new ChessPiece(ChessPieceId.WQB, ChessTypeId.Bishop, new ChessPosition(2,0)),
            new ChessPiece(ChessPieceId.WQ, ChessTypeId.Queen,   new ChessPosition(3,0)),
            new ChessPiece(ChessPieceId.WK, ChessTypeId.King,    new ChessPosition(ChessPosition.kXK,0)),
            new ChessPiece(ChessPieceId.WKB, ChessTypeId.Bishop, new ChessPosition(5,0)),
            new ChessPiece(ChessPieceId.WKN, ChessTypeId.Knight, new ChessPosition(6,0)),
            new ChessPiece(ChessPieceId.WKR, ChessTypeId.Rook,   new ChessPosition(7,0)),

            new ChessPiece(ChessPieceId.WPa, ChessTypeId.Pawn,   new ChessPosition(0,1)),
            new ChessPiece(ChessPieceId.WPb, ChessTypeId.Pawn,   new ChessPosition(1,1)),
            new ChessPiece(ChessPieceId.WPc, ChessTypeId.Pawn,   new ChessPosition(2,1)),
            new ChessPiece(ChessPieceId.WPd, ChessTypeId.Pawn,   new ChessPosition(3,1)),
            new ChessPiece(ChessPieceId.WPe, ChessTypeId.Pawn,   new ChessPosition(4,1)),
            new ChessPiece(ChessPieceId.WPf, ChessTypeId.Pawn,   new ChessPosition(5,1)),
            new ChessPiece(ChessPieceId.WPg, ChessTypeId.Pawn,   new ChessPosition(6,1)),
            new ChessPiece(ChessPieceId.WPh, ChessTypeId.Pawn,   new ChessPosition(7,1)),

            new ChessPiece(ChessPieceId.BQR, ChessTypeId.Rook,   new ChessPosition(0,7)),
            new ChessPiece(ChessPieceId.BQN, ChessTypeId.Knight, new ChessPosition(1,7)),
            new ChessPiece(ChessPieceId.BQB, ChessTypeId.Bishop, new ChessPosition(2,7)),
            new ChessPiece(ChessPieceId.BQ, ChessTypeId.Queen,   new ChessPosition(3,7)),
            new ChessPiece(ChessPieceId.BK, ChessTypeId.King,    new ChessPosition(ChessPosition.kXK,7)),
            new ChessPiece(ChessPieceId.BKB, ChessTypeId.Bishop, new ChessPosition(5,7)),
            new ChessPiece(ChessPieceId.BKN, ChessTypeId.Knight, new ChessPosition(6,7)),
            new ChessPiece(ChessPieceId.BKR, ChessTypeId.Rook,   new ChessPosition(7,7)),

            new ChessPiece(ChessPieceId.BPa, ChessTypeId.Pawn,   new ChessPosition(0,6)),
            new ChessPiece(ChessPieceId.BPb, ChessTypeId.Pawn,   new ChessPosition(1,6)),
            new ChessPiece(ChessPieceId.BPc, ChessTypeId.Pawn,   new ChessPosition(2,6)),
            new ChessPiece(ChessPieceId.BPd, ChessTypeId.Pawn,   new ChessPosition(3,6)),
            new ChessPiece(ChessPieceId.BPe, ChessTypeId.Pawn,   new ChessPosition(4,6)),
            new ChessPiece(ChessPieceId.BPf, ChessTypeId.Pawn,   new ChessPosition(5,6)),
            new ChessPiece(ChessPieceId.BPg, ChessTypeId.Pawn,   new ChessPosition(6,6)),
            new ChessPiece(ChessPieceId.BPh, ChessTypeId.Pawn,   new ChessPosition(7,6)),
        };

        public override int GetHashCode() // struct
        {
            return IdIdx;
        }
        public override bool Equals(object other) // struct
        {
            return IdIdx == ((ChessPiece)other).IdIdx;
        }
        public static bool operator ==(ChessPiece c1, ChessPiece c2) // struct
        {
            return c1.IdIdx == c2.IdIdx;
        }
        public static bool operator !=(ChessPiece c1, ChessPiece c2) // struct
        {
            return c1.IdIdx != c2.IdIdx;
        }

        public static readonly ChessPiece kNull = new ChessPiece(ChessPieceId.QTY, ChessTypeId.QTY, ChessPosition.kNull); // only use this if ChessPiece is struct.

        public ChessPiece(ChessPiece clone)
        {
            IdIdx = clone.IdIdx;
            TypeId = clone.TypeId;
            Pos = clone.Pos;
        }
        public ChessPiece(ChessPieceId id, ChessTypeId type, ChessPosition pos)
        {
            IdIdx = (byte)id;
            TypeId = type;
            Pos = pos;
        }
        public ChessPiece(ChessPieceId id, ChessPosition pos)
        {
            IdIdx = (byte)id;
            TypeId = kInitPieces[(byte)id].TypeId;
            Pos = pos;
        }
        public ChessPiece(ChessPieceId id)
        {
            IdIdx = (byte)id;
            TypeId = kInitPieces[(byte)id].TypeId;
            Pos = kInitPieces[(byte)id].Pos;
        }
    }
}
