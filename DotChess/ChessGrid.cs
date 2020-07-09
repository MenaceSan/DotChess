//  
// Copyright (c) 2020 Dennis Robinson (www.menasoft.com). All rights reserved.  
// Licensed under the MIT License. See ReadMe.md file in the project root for full license information.  
// 
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DotChess
{
    /// <summary>
    /// A grid of squares. What piece is in each square?
    /// 8x8 2d array. 0 = unoccupied space. else ChessPieceId + kSquare1 [kDim][kDim]
    /// Similar to https://en.wikipedia.org/wiki/Bitboard (But we don't use bit masks)
    /// </summary>
    public class ChessGrid
    {
        private readonly byte[] Squares = new byte[ChessPosition.kDim * ChessPosition.kDim];    // 8x8 2d array. 0 = unoccupied space. else index ChessPieceId + kSquare1 [kDim][kDim]
        private const byte kSquareEmpty = 0;
        private const byte kSquare1 = 1;  // 1 based offset to ChessPieceId in Squares array.

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsEmpty(byte x, byte y)
        {
            return Squares[ChessPosition.GetBitIdx(x, y)] == kSquareEmpty;
        }

        /// <summary>
        /// get ChessPieceId index at position.
        /// </summary>
        /// <param name="x">byte</param>
        /// <param name="y">byte</param>
        /// <returns>-1 = empty; else ChessPieceId</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetAt(byte x, byte y)
        {
            int idb = Squares[ChessPosition.GetBitIdx(x,y)];
            return idb - kSquare1;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetAt(ChessPosition pos)
        {
            return GetAt(pos.X, pos.Y);
        }

        private void SetAt1(ChessPosition pos, byte index1)
        {
            Squares[pos.BitIdx] = index1;
        }

        public void SetAt(ChessPosition pos, ChessPieceId id)
        {
            // Set a piece in a square. Assume caller will clear its old location if necessary.
            SetAt1(pos, (byte)(id + kSquare1));
        }
        public void ClearAt(ChessPosition pos)
        {
            // Mark the square as empty.
            SetAt1(pos, kSquareEmpty);
        }

        public ChessGrid(ChessPiece[] pieces)
        {
            // Populate the grid at start.
            Debug.Assert(pieces.Length <= (byte)ChessPieceId.QTY);

            for (byte id = 0; id < pieces.Length; id++)
            {
                ChessPiece piece = pieces[id];
                Debug.Assert(piece.IdIdx == id);  // sanity check.
                if (!piece.IsOnBoard)
                    continue;
                SetAt(piece.Pos, piece.Id);
            }
        }
    }
}
