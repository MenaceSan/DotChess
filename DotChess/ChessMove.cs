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
    /// A game half-move/Ply for a single predefined/assumed piece, given a certain board state.
    /// </summary>
    [Serializable]
    public class ChessMove
    {
        public ChessPosition ToPos;   // Move a piece to here. 2 bytes.
        public ChessResultF Flags;    // What is the result of this move. 2 bytes.

        public bool IsValid => ToPos.IsOnBoard && Flags.IsAllowedMove();

        public static int Compare2(ChessMove a, ChessMove b)
        {
            // For sorting in a list. 
            int diff = (int)a.Flags - (int)b.Flags;
            if (diff != 0)
                return diff;
            return ChessPosition.Compare2(a.ToPos, b.ToPos);
        }
        public bool Equals1(ChessMove other)
        {
            // like IEquatable<>
            return ToPos.Equals(other.ToPos) && Flags == other.Flags;
        }

        public ChessMove(ChessPosition toPos, ChessResultF flags)
        {
            ToPos = toPos;
            Flags = flags;
        }
        public ChessMove(ChessMove clone)
        {
            ToPos = clone.ToPos;
            Flags = clone.Flags;
        }
    }

    /// <summary>
    /// A game move of a specific ChessPieceId given a certain board state.
    /// </summary>
    [Serializable]
    public class ChessMoveId : ChessMove
    {
        public ChessPieceId Id;     // move this specific piece. ChessPieceId.QTY = unknown.

        public bool IsWhite => ChessPiece.IsWhite(Id);

        public bool Equals(ChessMoveId other)
        {
            // like IEquatable<>
            return Id == other.Id && base.Equals1(other);
        }
        public static int Compare2(ChessMoveId a, ChessMoveId b)
        {
            // For sorting in a list. 
            int diff = (int)a.Id - (int)b.Id;
            if (diff != 0)
                return diff;
            return ChessMove.Compare2(a, b);
        }

        public ChessMoveId()
            : base(ChessPosition.kNull, ChessResultF.OK)
        {
            Id = ChessPieceId.QTY;
        }
        public ChessMoveId(ChessPieceId id, ChessPosition toPos, ChessResultF flags)
            : base(toPos, flags)
        {
            Id = id;
        }
        public ChessMoveId(ChessMove clone, ChessPieceId id)
           : base(clone)
        {
            Id = id;
        }
    }

    /// <summary>
    /// A White Winning Move from a board state. has an associated HashCode64. Store as 16 bytes.
    /// All History moves are recorded as White point of view. Transpose to use for Black.
    /// Record only game winning moves.
    /// </summary>
    public class ChessMoveHistory : ChessMoveId
    {
        public ulong HashCode;          // Hash for the board state BEFORE the move. 
        // public ChessGameInfo GameInfo;     // what historical game did this move belong to?

        public bool Equals(ChessMoveHistory other)
        {
            // like IEquatable<>
            return HashCode == other.HashCode && base.Equals(other);
        }

        public static int Compare2(ChessMoveHistory a, ChessMoveHistory b)
        {
            // For sorting in a list. 
            int diff = Comparer<ulong>.Default.Compare(a.HashCode, b.HashCode);
            if (diff != 0)
                return diff;
            return ChessMoveId.Compare2(a, b);
        }

        public ChessMoveHistory(ulong hashCode, ChessPieceId id, ChessPosition toPos, ChessResultF flags)
            : base(id, toPos, flags)
        {
            HashCode = hashCode;
        }
    }

    /// <summary>
    /// DB of historical moves. White Winning Moves. Usually used for opening moves.
    /// opening book
    ///  https://www.chessgames.com/
    ///  https://sourceforge.net/projects/codekiddy-chess/
    ///  https://thechessworld.com/articles/general-information/15-best-chess-games-of-all-time-annotated/
    /// </summary>
    public class ChessDb
    {
        public const string kOpeningDbFile = "OpeningDb.bin";       // All moves recorded as whites winning moves. Transpose to use for black.
        public const int kOpeningMoves = 18;   // How many moves into the game 9B+9W for historic games in kOpeningDbFile?

        public List<ChessMoveHistory> Moves = new List<ChessMoveHistory>();     // sorted by board state ulong HashCode. may have dupes.

        /// <summary>
        /// IComparer for sorting.
        /// </summary>
        private class CompSort : IComparer<ChessMoveHistory>
        {
            public int Compare(ChessMoveHistory a, ChessMoveHistory b)
            {
                return ChessMoveHistory.Compare2(a, b);
            }
        }
        private static readonly CompSort _CompSort = new CompSort();

        /// <summary>
        /// IComparer for BinarySearch finding by HashCode.
        /// </summary>
        private class CompFind : IComparer<ChessMoveHistory>
        {
            public int Compare(ChessMoveHistory a, ChessMoveHistory b)
            {
                return Comparer<ulong>.Default.Compare(a.HashCode, b.HashCode);
            }
        }
        private static readonly CompFind _CompFind = new CompFind();

        public static ChessDb _Instance;    // Singleton.

        public void AddGameMove(ulong hashCode64, ChessMoveId moveId, ChessGameInfo info, bool transpose)
        {
            // Add a White Winning Move to the db. Thread Safe.
            // hashCode64 = config of the board BEFORE the move as a hash code.

            lock (Moves)
            {
                var moveH = new ChessMoveHistory(hashCode64,
                    transpose ? ChessPiece.GetTransposed(moveId.Id) : moveId.Id,
                    transpose ? moveId.ToPos.GetTransposed() : moveId.ToPos,
                    moveId.Flags
                    );

                Debug.Assert(moveH.IsValid && moveH.IsWhite);
                Moves.Add(moveH);
            }
        }

        public void SortMoves()
        {
            Moves.Sort(_CompSort);
        }

        const uint kHeader = 0x12345678;    // declare the format/type of this file.
        const uint kVer = 0x00010010;       // Version + row width.

        public void ReadDb(string dir)
        {
            // Read/Load the db file

            Moves.Clear();
            if (dir == "")
                return;

            string path = Path.Combine(dir, kOpeningDbFile);
            if (!File.Exists(path))
                return;

            using (var h = File.Open(path, FileMode.Open))
            {
                using (var reader = new BinaryReader(h))
                {
                    long length = reader.BaseStream.Length;
                    if (length <= 0)
                        return;
                    long pos1 = reader.BaseStream.Position;

                    // Read header.
                    uint code1 = reader.ReadUInt32();    // 4
                    if (code1 != kHeader)
                    {
                        ChessGame.InternalFailure("kHeader");
                        return;
                    }
                    uint code2 = reader.ReadUInt32();    // 4
                    if (code2 != kVer)
                    {
                        ChessGame.InternalFailure("kVer");
                        return;
                    }
                    ulong padding8 = reader.ReadUInt64();    // 8
                    long pos2 = reader.BaseStream.Position;

                    while (pos2 < length)
                    {
                        var hashCode = reader.ReadUInt64();    // 8    // write reversed ?;
                        var id = (ChessPieceId)reader.ReadByte(); // 1
                        byte x = reader.ReadByte();     // 1
                        byte y = reader.ReadByte();     // 1
                        var flags = (ChessResultF)reader.ReadUInt16(); // 2
                        int padding1 = reader.ReadByte(); // 1 
                        int padding2 = reader.ReadUInt16(); // 2

                        var move = new ChessMoveHistory(hashCode, id, new ChessPosition(x, y), flags);
                        Debug.Assert(move.IsValid && move.IsWhite);
                        Moves.Add(move);

                        pos1 = pos2;
                        pos2 = reader.BaseStream.Position;
                    }
                }
            }

            SortMoves();    // should already be sorted.
        }

        public static void OpenDbFile(string dir)
        {
            if (_Instance != null)
                return;
            _Instance = new ChessDb();
            _Instance.ReadDb(dir);      // read if it exists.
        }

        public void WriteDb(string dir)
        {
            SortMoves();    // should already be sorted?

            string path = Path.Combine(dir, kOpeningDbFile);
            using (var h = File.Open(path, FileMode.Create))
            {
                using (var writer = new BinaryWriter(h))
                {
                    // First record is header.
                    writer.Write(kHeader);    // 4
                    writer.Write(kVer);    // 4
                    writer.Write((ulong)0);    // 8 padding

                    ChessMoveHistory moveLast = null;
                    foreach (ChessMoveHistory move in Moves)
                    {
                        if (moveLast != null && move.Equals(moveLast))  // skip full dupes.
                            continue;

                        Debug.Assert(move.IsValid && move.IsWhite);

                        writer.Write(move.HashCode);    // 8
                        writer.Write((byte)move.Id);    // 1
                        writer.Write(move.ToPos.X);   // 1
                        writer.Write(move.ToPos.Y);   // 1
                        writer.Write((ushort)move.Flags);   // 2
                        writer.Write((byte)0);  // 1 padding
                        writer.Write((ushort)0);    // 2 padding

                        moveLast = move;
                    }
                }
            }
        }

        public static void WriteDbFile(string dir)
        {
            if (_Instance == null)
            {
                _Instance = new ChessDb();
            }
            _Instance.WriteDb(dir);
        }

        public List<ChessMoveHistory> FindMoves(ulong hashCode, bool transpose)
        {
            // Get White Winning Moves for this board state hashCode.

            var moves = new List<ChessMoveHistory>();
            var find = new ChessMoveHistory(hashCode, ChessPieceId.QTY, ChessPosition.kNull, ChessResultF.OK);
            int i = Moves.BinarySearch(find, _CompFind);
            if (i < 0)
                return moves;    // has none.

            // find first match.
            for (int j = i - 1; j >= 0 && Moves[j].HashCode == hashCode; j--)
            {
                moves.Add(Moves[j]);
            }
            for (; i < Moves.Count && Moves[i].HashCode == hashCode; i++)
            {
                moves.Add(Moves[i]);
            }

            if (transpose)  // transpose the results so black can use them.
            {
                for (int k = 0; k < moves.Count; k++)
                {
                    var move = moves[k];
                    moves[k] = new ChessMoveHistory(hashCode, ChessPiece.GetTransposed(move.Id), move.ToPos.GetTransposed(), move.Flags);
                    Debug.Assert(moves[k].IsValid && moves[k].IsWhite != transpose);
                }
            }
            return moves;
        }
    }
}
