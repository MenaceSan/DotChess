//  
// Copyright (c) 2020 Dennis Robinson (www.menasoft.com). All rights reserved.  
// Licensed under the MIT License. See ReadMe.md file in the project root for full license information.  
// 
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace DotChess
{
    /// <summary>
    /// The status of the board.
    /// NO Validate of moves.
    /// </summary>
    public class ChessBoard
    {
        public readonly ChessPiece[] Pieces = new ChessPiece[(byte)ChessPieceId.QTY];  // 32 pieces by Id. ChessPieceId enum [32 = ChessPieceId.QTY = kDim * 4]. captured or in play on board.
        protected readonly ChessGrid Grid;
        public ChessPlayState State;

        // derived/helper values.
        public byte CaptureCount;    // Quantity of pieces captured/off board. 

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected ChessPiece GetPiece(byte id)
        {
            return Pieces[id];
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public ChessPiece GetPiece(ChessPieceId id)
        {
            return GetPiece((byte)id);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdatePiece(ChessPiece piece)
        {
            // The piece changed its ChessTypeId or ChessPosition.
            // If the piece is a struct we must use this, object/reference types won't need this
            Pieces[piece.IdIdx] = piece;   // update struct.
        }

        protected void SetPawnType(ChessPiece piece, ChessTypeId typeId)
        {
            // Pawn promotion.
            piece = GetPiece(piece.Id);
            piece.TypeId = typeId;    // set pawns new type.
            UpdatePiece(piece);
        }

        private ChessPiece GetCaptured(byte captureCount)
        {
            // Find a specific captured piece.
            // can return ChessPiece.kNull.

            foreach (ChessPiece piece in Pieces)
            {
                if (piece == ChessPiece.kNull)
                    continue;
                if (!piece.IsOnBoard && piece.Pos.GetCaptureCount() == captureCount)
                    return piece;
            }
            return ChessPiece.kNull;
        }

        protected void SetCaptured(ChessPiece piece, byte captureCount)
        {
            // ASSUME caller doesn't care about piece anymore.
            Debug.Assert(captureCount < (byte)ChessPieceId.QTY);
            piece.Pos = new ChessPosition(captureCount);
            UpdatePiece(piece);
        }

        protected void SetCaptured(ChessPiece piece)
        {
            // Put captured piece in a free captured spot.
            for (byte i = 0; i < (byte)ChessPieceId.QTY; i++)
            {
                if (GetCaptured(CaptureCount) == ChessPiece.kNull)
                {
                    SetCaptured(piece, CaptureCount++);
                    return;
                }
                if (++CaptureCount >= (byte)ChessPieceId.QTY)
                    CaptureCount = 0; // Restart from beginning.
            }
            ChessGame.InternalFailure("SetCaptured");
        }

        protected ChessPiece GetAt(byte x, byte y)
        {
            // Get a piece on the board.
            // can return ChessPiece.kNull.
            int id = Grid.GetAt(x, y);
            if (id < 0)
                return ChessPiece.kNull;
            return GetPiece((ChessPieceId)id);
        }
        public ChessPiece GetAt(ChessPosition pos)
        {
            // Get a piece on the board.
            // Debug.Assert(pos.IsOnBoard);
            return GetAt(pos.X, pos.Y);
        }
        private ChessPiece GetAt2(ChessPosition pos)
        {
            // Get a piece on the board or captured. May return ChessPiece.kNull
            if (pos.IsOnBoard)
            {
                // on board.
                return GetAt(pos);
            }
            else
            {
                // in capture spot.
                return GetCaptured(pos.GetCaptureCount());
            }
        }

        protected void SetAt1(ChessPiece piece, ChessPosition pos)
        {
            // Debug.Assert(pos.IsOnBoard);
            Grid.SetAt(pos, piece.Id);
            piece.Pos = pos;
            UpdatePiece(piece);
        }

        protected void SetAt2(ChessPiece piece, ChessPosition posNew)
        {
            // ASSUME new ChessPosition is empty (not collision). (or will be cleaned up)
            Grid.ClearAt(piece.Pos);  // clear previous spot.
            SetAt1(piece, posNew);
        }

        /// <summary>
        /// Is the Grid state valid ? "Problem: X"
        /// </summary>
        /// <returns>Error Message or null</returns>

        protected string GetGridError()
        {
            List<ChessPiece> pieces = Pieces.ToList();  // clone Pieces.

            for (byte y = 0; y < ChessPosition.kDim; y++)
            {
                for (byte x = 0; x < ChessPosition.kDim; x++)
                {
                    ChessPiece piece = GetAt(x, y);
                    if (piece == ChessPiece.kNull)
                        continue;
                    if (piece.Pos.X != x || piece.Pos.Y != y || !piece.IsOnBoard)
                        return "Grid/piece mismatch";
                    pieces.Remove(piece);   // accounted for.
                }
            }

            // All pieces NOT on the board should be in captured list.
            foreach (ChessPiece piece in pieces)
            {
                if (piece.IsOnBoard)
                    return "Piece not on grid";
            }

            return null;
        }

        protected string GetPiecesError()
        {
            // Maybe this should be in ChessGameBoard ? Game rules related stuff ?
            ulong captureBitMask = 0;   // capture spots must be free.
            byte captureCount = 0;
            byte onBoardCount = 0;

            if (Pieces.Length < (byte)ChessPieceId.QTY)
                return "Number of Pieces";

            for (byte id = 0; id < (byte)ChessPieceId.QTY; id++)
            {
                ChessPiece piece = GetPiece(id);
                if (piece == ChessPiece.kNull)
                    return $"Piece '{id}' missing";
                if (piece.IdIdx != id)
                    return $"Piece '{id}' Id wrong. '{piece.Id}'";

                ChessTypeId typeInit = piece.Init.TypeId;
                if (piece.TypeId != typeInit)
                {
                    if (typeInit != ChessTypeId.Pawn)   // only pawns can change type.
                        return $"Piece '{id}' is type {piece.TypeId}";
                    if (piece.TypeId == ChessTypeId.King) // cant promote to king.
                        return "Pawn as King";
                    if (!ChessType.IsValidId(piece.TypeId)) // bad TypeId should never happen.
                        return "Pawn has Invalid TypeId";
                }

                if (piece.IsOnBoard)
                {
                    int id2 = Grid.GetAt(piece.Pos);
                    if (id2 != id)
                        return "Grid id mismatch";
                    onBoardCount++;
                    continue;
                }

                if (piece.IsKing)
                    return "Captured King";
                if (!piece.Pos.IsValidCaptured)
                {
                    return "Captured Pos";
                }

                byte captureCount2 = piece.Pos.GetCaptureCount();
                if (captureCount2 >= ChessPosition.kNullVal)
                    return "Captured Count";

                ulong bitMask = 1UL << captureCount2;
                if ((captureBitMask & bitMask) != 0)
                {
                    return "CaptureCount collision";
                }
                captureBitMask |= bitMask;
                captureCount++;
            }

            if ((captureCount + onBoardCount) != (byte)ChessPieceId.QTY)
            {
                return "Piece count Bad";
            }

            return null;    // OK
        }

        public void MoveX(ChessPiece piece, ChessPosition posNew)
        {
            // Move without any rules. exchange if we collide.

            if (posNew.Equals(piece.Pos))
                return;

            ChessPiece pieceX = GetAt2(posNew);   // is collision?
            if (pieceX == ChessPiece.kNull)
            {
                if (piece.IsOnBoard)
                {
                    Grid.ClearAt(piece.Pos); // clear my old spot.
                }
            }
            else
            {
                // collision. swap pieceX.
                if (piece.IsOnBoard)
                    SetAt1(pieceX, piece.Pos);
                else
                    SetCaptured(pieceX, piece.Pos.GetCaptureCount());
            }

            if (posNew.IsOnBoard)
                SetAt1(piece, posNew);
            else
                SetCaptured(piece, posNew.GetCaptureCount());
        }

        //*************************************************************************

        const char kFen1Sep = '/';      // separator inside the first segment.

        public static void TransposeFEN1(StringBuilder sb)
        {
            // transpose (reverse) FEN (first part).
            // Type moves for black are the same as equiv white moves for the transposed board state.
            int len2 = sb.Length / 2;
            int j = sb.Length - 1;
            for (int i = 0; i < len2; i++, j--)
            {
                char ch1 = ChessColor.ToFenTranspose(sb[i]);
                sb[i] = ChessColor.ToFenTranspose(sb[j]);
                sb[j] = ch1;
            }
        }

        public void GetFEN1(StringBuilder sb)
        {
            // get a string that describes piece types at locations on the board. else assumed captured.
            // From White point of view. top to bottom

            for (byte dy = ChessPosition.kDim; dy > 0;)
            {
                dy--;
                int countEmpty = 0;
                for (byte dx = 0; dx < ChessPosition.kDim; dx++)
                {
                    ChessPiece piece = GetAt(dx, dy);
                    if (piece == ChessPiece.kNull)
                    {
                        countEmpty++;
                        continue;
                    }
                    if (countEmpty > 0)
                    {
                        sb.Append(ChessUtil.ToChar(countEmpty));
                        countEmpty = 0;
                    }
                    sb.Append(piece.FEN);
                }
                if (countEmpty > 0)
                {
                    sb.Append(ChessUtil.ToChar(countEmpty));
                }
                if (dy > 0)
                    sb.Append(kFen1Sep);
            }
        }

        public string GetFEN(bool useCaptureOrder)
        {
            // useCaptureOrder = CaptureCount values.
            // Get FEN string. a standard string that represents the state of the board and game.
            // https://en.wikipedia.org/wiki/Forsyth%E2%80%93Edwards_Notation
            // e.g. starting pos = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1"

            var sb = new StringBuilder();

            GetFEN1(sb);
            sb.Append(ChessUtil.kFenSep);
            State.GetFEN(sb);

            if (useCaptureOrder)
            {
                // record the captured pieces.
                sb.Append(ChessUtil.kFenSep);
                for (byte i = 0; i < (byte)ChessPieceId.QTY; i++)
                {
                    ChessPiece pieceCap = GetCaptured(i);
                    if (pieceCap == ChessPiece.kNull)
                        continue;
                    sb.Append(pieceCap.FEN);
                }
            }
            return sb.ToString();
        }

        public string FEN
        {
            get
            {
                return GetFEN(false);
            }
        }

        public override string ToString()
        {
            return GetFEN(true);
        }

        public string GetStateString()
        {
            // Get the restore-able state of the board as a simple string. Simpler(more specific) than FEN.

            var sb = new StringBuilder();

            foreach (ChessPiece piece in Pieces)
            {
                if (piece.IsPawnId && !piece.IsPawnType)    // Promoted?
                {
                    sb.Append(piece.Type.TypeChar);
                }
                sb.Append(piece.Pos.Notation);
            }

            State.GetStateString(sb);

            return sb.ToString();
        }

        public const ulong kHash0 = 0x86f926c26a253d57;     // start for White. NOT transposed. NOTE: If this changes then we must rebuild the Opening Moves DB !

        public ulong GetHashCode64(bool transpose)
        {
            // get the state of the board/game as a 64 bit hash code.
            // transpose = black view of the board would be equiv to the white view but transposed.
            // Id is not important, only TypeId and position.
            // NOTE: ANY changes here invalidates the db hashes.

            ulong hashedValue = ChessUtil.kHashValue1;
            uint countEmpty = 0;
            if (transpose)
            {
                for (byte dy = 0; dy < ChessPosition.kDim; dy++)
                {
                    for (byte dx = ChessPosition.kDim; dx > 0;)
                    {
                        dx--;
                        ChessPiece piece = GetAt(dx, dy);
                        if (piece == ChessPiece.kNull)
                        {
                            countEmpty++;
                            continue;
                        }
                        if (countEmpty > 0)
                        {
                            hashedValue += countEmpty;
                            hashedValue *= ChessUtil.kHashValue2;
                            countEmpty = 0;
                        }
                        hashedValue += (uint)piece.TypeId;
                        if (ChessPiece.IsWhite(piece.Id))
                            hashedValue += (uint)ChessTypeId.QTY;
                        hashedValue *= ChessUtil.kHashValue2;
                    }
                }
            }
            else
            {
                for (byte dy = ChessPosition.kDim; dy > 0;)
                {
                    dy--;
                    for (byte dx = 0; dx < ChessPosition.kDim; dx++)
                    {
                        ChessPiece piece = GetAt(dx, dy);
                        if (piece == ChessPiece.kNull)
                        {
                            countEmpty++;
                            continue;
                        }
                        if (countEmpty > 0)
                        {
                            hashedValue += countEmpty;
                            hashedValue *= ChessUtil.kHashValue2;
                            countEmpty = 0;
                        }
                        hashedValue += (uint)piece.TypeId;
                        if (!ChessPiece.IsWhite(piece.Id))
                            hashedValue += (uint)ChessTypeId.QTY;
                        hashedValue *= ChessUtil.kHashValue2;
                    }
                }
            }

            if (countEmpty > 0)
            {
                hashedValue += countEmpty;
                hashedValue *= ChessUtil.kHashValue2;
            }

            hashedValue = State.GetHashCode64(hashedValue, transpose);

            return hashedValue;
        }

        //*******************************************************

        private void InitPiece(ChessPiece piece)
        {
            Pieces[piece.IdIdx] = piece;
        }

        private void InitPiecesNull()
        {
            for (byte id = 0; id < (byte)ChessPieceId.QTY; id++)
            {
                Pieces[id] = ChessPiece.kNull;
            }
        }

        private void InitPiecesPartial()
        {
            // Fill in any missing pieces as captured pieces.
            // called after InitPiecesNull()

            for (byte id = 0; id < (byte)ChessPieceId.QTY; id++)
            {
                ChessPiece piece = GetPiece(id);
                if (piece == ChessPiece.kNull)
                {
                    piece = new ChessPiece((ChessPieceId)id);
                    InitPiece(piece);
                    SetCaptured(piece);
                }
            }
        }

        private void InitPiecesClone(ChessPiece[] pieces)
        {
            // Clone all pieces. Assume all pieces are present.

            for (byte id = 0; id < (byte)ChessPieceId.QTY; id++)
            {
                Debug.Assert(id == pieces[id].IdIdx);
                InitPiece(new ChessPiece(pieces[id]));    // clone ChessPiece
            }
        }

        private byte InitFENPieceId(ChessTypeId typeId, ChessColor color)
        {
            // Get a ChessPieceId that is not currently in use.
            // RETURN: ChessPieceId.QTY = invalid.

            // Find unused piece of this type.
            byte id = 0;
            for (; id < (byte)ChessPieceId.QTY; id++)
            {
                ChessPiece pieceInit = ChessPiece.kInitPieces[id];
                if (GetPiece(id) == ChessPiece.kNull && pieceInit.IsMatch(typeId, color)) // Has not already been created.
                    return id;
            }

            if (typeId != ChessTypeId.Pawn)
            {
                // Create an upgraded pawn.
                id = InitFENPieceId(ChessTypeId.Pawn, color);
            }

            return id;
        }

        private void InitFEN(string boardFen)
        {
            int i = 0;
            byte x = 0;
            byte y = ChessPosition.kDim1;
            for (; i < boardFen.Length; i++)
            {
                char ch = boardFen[i];
                if (ch == kFen1Sep)
                {
                    if (y == 0)
                        break;
                    y--;
                    x = 0;
                    continue;
                }
                if (char.IsNumber(ch))
                {
                    // skip empty spaces.
                    x += (byte)(ch - '0');
                }
                else
                {
                    int typeId = ChessType.GetTypeIdFrom(char.ToUpper(ch));
                    if (typeId < 0)
                    {
                        ChessGame.InternalFailure("Bad FEN char.");
                        continue;
                    }
                    byte id = InitFENPieceId((ChessTypeId)typeId, ChessColor.GetColorFromChar(ch));
                    if (!ChessPiece.IsValidId((ChessPieceId)id))
                    {
                        ChessGame.InternalFailure("Bad FEN char.");
                        continue;
                    }
                    InitPiece(new ChessPiece((ChessPieceId)id, (ChessTypeId)typeId, new ChessPosition(x, y)));
                    x++;
                }
            }
        }

        public ChessBoard(string[] fen, int i = 0, bool hasOrder = true)
        {
            // Set the board state as a single string. reverse of FEN GetFEN

            InitPiecesNull();

            // Parse the first part of the FEN string.
            InitFEN(fen[i]);

            // Parse the rest of the FEN string. 5 more parts.
            if (fen.Length - i > 1)
            {
                State = new ChessPlayState(fen, i+1);
            }
            else
            {
                State = new ChessPlayState();
            }

            // Parse capture order (if present). GetFEN(useCaptureOrder)
            if (hasOrder && fen.Length - i > 6)
            {
                foreach (char ch in fen[i+6])
                {
                    int typeId = ChessType.GetTypeIdFrom(char.ToUpper(ch));
                    if (typeId < 0)
                    {
                        ChessGame.InternalFailure("ChessBoard fen 6");
                        break;
                    }
                    int id = InitFENPieceId((ChessTypeId)typeId, ChessColor.GetColorFromChar(ch));
                    if (id < 0)
                    {
                        ChessGame.InternalFailure("Bad FEN char.");
                        continue;
                    }
                    var piece = new ChessPiece((ChessPieceId)id, (ChessTypeId)typeId, ChessPosition.kNull);
                    InitPiece(piece);
                    SetCaptured(piece);
                }
            }

            InitPiecesPartial();
            Grid = new ChessGrid(Pieces);  // Lastly. Populate grid squares.                                                 

            // Fix all the init positions. Prefer PosInit for proper CastleFlags.
            foreach (ChessPiece piece in Pieces)
            {
                if (piece == null)
                    continue;
                ChessPiece piece2 = GetAt(piece.InitPos);
                if (piece2 == ChessPiece.kNull || piece2 == piece || !piece2.IsMatch(piece.TypeId, piece.Color))
                    continue;
                MoveX(piece, piece.InitPos); // Swap
                Debug.Assert(GetPiece(piece.Id).IsInitPos);
            }
        }

        public ChessBoard(string stateString)
        {
            // restore/Parse from GetStateString()

            int i = 0;
            for (byte id = 0; id < (byte)ChessPieceId.QTY; id++)
            {
                if (i >= stateString.Length)
                {
                    ChessGame.InternalFailure("ChessBoard StateString Len");
                    return;
                }

                ChessTypeId typeId0 = ChessPiece.kInitPieces[id].TypeId;

                int typeId = ChessType.GetTypeIdFrom(stateString[i]);
                if (typeId >= 0 && typeId0 == ChessTypeId.Pawn)
                {
                    i++;    // promoted pawn.
                }
                else
                {
                    typeId = (int)typeId0;
                }

                var pos = new ChessPosition(stateString[i + 0], stateString[i + 1]);
                InitPiece(new ChessPiece((ChessPieceId)id, (ChessTypeId)typeId, pos));
                i += 2;
            }

            State = new ChessPlayState(stateString, i);
            InitPiecesPartial();
            Grid = new ChessGrid(Pieces);  // Lastly. Populate grid squares.                                                 
        }

        public ChessBoard(IEnumerable<ChessPiece> pieces)
        {
            // Use provided pieces to set up the board. NOT a clone. may be partial set of pieces.
            InitPiecesNull();
            State = new ChessPlayState();

            foreach (ChessPiece piece in pieces)
            {
                InitPiece(piece);
                if (!piece.IsOnBoard)
                {
                    ++CaptureCount;
                }
            }

            InitPiecesPartial();
 
            Grid = new ChessGrid(Pieces);  // Lastly. Populate grid squares.                                                 
        }

        public ChessBoard(ChessBoard clone)
        {
            // Make a clone copy of the state of this board so i can try other moves?
            // like ICloneable object
            State = new ChessPlayState(clone.State);
            InitPiecesClone(clone.Pieces);
            CaptureCount = clone.CaptureCount;
            Grid = new ChessGrid(Pieces);  // Lastly. Populate grid squares.                                                 
        }

        public ChessBoard()
        {
            // Create a new board.
            State = new ChessPlayState();
            InitPiecesClone(ChessPiece.kInitPieces);
            Grid = new ChessGrid(Pieces);  // Lastly. Populate grid squares.                                                                                            
        }
    }
}
