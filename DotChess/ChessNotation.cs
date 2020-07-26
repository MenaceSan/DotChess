//  
// Copyright (c) 2020 Dennis Robinson (www.menasoft.com). All rights reserved.  
// Licensed under the MIT License. See ReadMe.md file in the project root for full license information.  
// 
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace DotChess
{
    /// <summary>
    /// Record history of a move as half line of Standard Notation. May be in PGN file.
    /// Allow to play back these moves.
    /// Get notation. http://www.chesscorner.com/tutorial/basic/notation/notate.htm
    /// </summary>
    [Serializable]
    public class ChessNotation1 : IEquatable<ChessNotation1>
    {
        /// <summary>
        /// What piece type this ? Id may not be defined. Type is Vague, Id is more specific. Use GetPiece() to resolve Id.
        /// </summary>
        public ChessTypeId TypeId = ChessTypeId.QTY;   // must be populated!
        public ChessType Type => ChessType.GetType(TypeId);

        /// <summary>
        /// Where did it move from? Can be Partial (X only, Y only) or empty. Assumes ToPos is the only valid move.
        /// </summary>
        public ChessPosition From = ChessPosition.kNull;    // no idea yet. Assume SetNotation() will be called.     

        public ChessMoveId Move = new ChessMoveId();    // Where to ?

        public const char kCommentChar = ';';   // line comment for PGN file.

        public const string kActive = "*";          // active game is not complete.
        public const string kWinWhite = "1-0";
        public const string kWinBlack = "0-1";
        public const string kStalemate = "1/2-1/2";     // 50 move rule or other reason.

        public const string kCastleQ = "O-O-O";
        public const string kCastleK = "O-O";

        public bool IsValid
        {
            get
            {
                if (Move.Flags.IsAny(ChessResultF.Resigned | ChessResultF.Stalemate))   // doesn't need any more. Game Over.
                    return true;
                if (!ChessType.IsValidId(TypeId))    // Must be valid.
                    return false;
                // From - Can be Partial (X or Y only) or empty.
                return Move.IsValid;
            }
        }

        public bool Equals(ChessNotation1 other)
        {
            // IEquatable<>
            // skip test for Id
            if (TypeId != other.TypeId)
                return false;
            if (!From.Equals(other.From))
                return false;
            return Move.Equals1(other.Move);
        }
        public bool Equals2(ChessNotation1 other)
        {
            if (Move.Id != other.Move.Id)
                return false;
            return Equals(other);
        }

        public override string ToString()
        {
            // Notation Symbol Meanings:  
            // R    Rook    
            // N    Knight  
            // B    Bishop
            // Q    Queen
            // K    King    
            // x    Captures
            // +    Check
            // ++ or #  Checkmate
            // -    moves to
            // O-O  Castles King's side	
            // O-O-O    Castles Queen's side

            var flags = Move.Flags;
            if (flags.IsAny(ChessResultF.Stalemate))
            {
                return kStalemate;
            }
            if (flags.IsAny(ChessResultF.Resigned))
            {
                // "1-0" or "0-1" 
                return flags.IsAny(ChessColor.kWhite.Flags) ? kWinBlack : kWinWhite;
            }

            if (flags.IsAny(ChessResultF.CastleK))
            {
                return kCastleK;
            }
            if (flags.IsAny(ChessResultF.CastleQ))
            {
                return kCastleQ;
            }

            var sb = new StringBuilder();

            if (TypeId != ChessTypeId.Pawn)
            {
                sb.Append(this.Type.TypeChar);
            }

            if (From.IsOnBoardX)
                sb.Append(From.NotationX);    // can be partial/empty.
            if (From.IsOnBoardY)
                sb.Append(From.NotationY);    // can be partial/empty.

            if (flags.IsAny(ChessResultF.Capture))
            {
                sb.Append('x');
            }
            else if (TypeId != ChessTypeId.Pawn && (From.IsOnBoardX || From.IsOnBoardY))
            {
                sb.Append('-'); // optional move char.
            }

            sb.Append(Move.ToPos.Notation);

            if (flags.IsAny(ChessResultF.PromoteQ))
            {
                // Q (promoting to queen).
                sb.Append("=Q");
            }
            if (flags.IsAny(ChessResultF.PromoteN))
            {
                // N (promoting to knight).
                sb.Append("=N");
            }
            if (flags.IsAny(ChessResultF.Checkmate))
            {
                sb.Append("#");
            }
            else if (flags.IsAny(ChessResultF.Check))
            {
                sb.Append('+');
            }
            if (flags.IsAny(ChessResultF.Good))
            {
                sb.Append("!");
            }
            if (flags.IsAny(ChessResultF.Bad))
            {
                sb.Append("?");
            }

            return sb.ToString();
        }

        public readonly char[] kWhiteSpace = new char[] { ' ', '\t' };

        static ChessResultF GetPromoteFlag(char ch)
        {
            switch (ch)
            {
                case 'Q':
                case 'B':
                case 'R': // same game play as a queen.
                    return ChessResultF.PromoteQ;
                case 'N':
                    return ChessResultF.PromoteN;
                default:
                    return 0;   // not valid.
            }
        }
        static bool IsPromoteChar(char ch)
        {
            return GetPromoteFlag(ch) != 0;
        }

        /// <summary>
        /// Parse http://www.chesscorner.com/tutorial/basic/notation/notate.htm
        /// read half a line of notation. http://www.chesscorner.com/tutorial/basic/notation/notate.htm
        /// e.g. "f2-f4 e7-e5"
        /// </summary>
        /// <param name="notation"></param>
        /// <param name="i">starting index in notation string</param>
        /// <param name="color"></param>
        /// <returns>index in notation string</returns>
        public int SetNotation(string notation, int i, ChessColor color)
        {
            // samples:
            // f4 = The pawn moves to f4 // Shorthand
            // Nf3 = The Knight moves to f3. // Shorthand
            // hxg3 = The pawn on the h file takes the XXX on g3 // Shorthand
            // Bxd6 = The Bishop takes the XXX on d6 // Shorthand
            // Rce7 = Rook on c goes to e7   
            // h2xg3
            // Qxg3+        // Shorthand.
            // Bxg3#    // Shorthand.
            // O-O
            // O-O-O

            Move.ToPos = ChessPosition.kNull;    // invalid

            if (i + 1 > notation.Length)
                return i;   // Not long enough to be valid.

            Move.Flags = color.Flags;

            int j = notation.IndexOfAny(kWhiteSpace, i);
            j = (j >= 0) ? j : notation.Length;
            Debug.Assert(j > i);

            string notation2 = notation.Substring(i, j - i);
            switch (notation2)   // needs to end in space/null.
            {
                case kWinBlack:
                    Move.Flags = ChessResultF.Resigned | ChessColor.kWhite.Flags;
                    return j;
                case kWinWhite:
                    Move.Flags = ChessResultF.Resigned | ChessColor.kBlack.Flags;
                    return j;
                case kStalemate:    // a Stalemate/draw 
                    Move.Flags = ChessResultF.Stalemate | color.Flags;
                    return j;
                case kActive:    // * = game is ongoing ?? No idea why Karpov uses this. assume game is a stalemate. no idea who wins.
                    Move.Flags = ChessResultF.Stalemate | color.Flags;
                    return j;
            }

            if (notation2.StartsWith(kCastleK))  // Short side ? or prefix of long side.
            {
                // Special King Move.
                TypeId = ChessTypeId.King;
                From = new ChessPosition(ChessPosition.kXK, color.RowKing);

                sbyte offsetX;
                if (notation2.StartsWith(kCastleQ))  // Long side
                {
                    Move.Flags |= ChessResultF.CastleQ;
                    i += kCastleQ.Length;
                    offsetX = ChessOffset.kCastleQ;
                }
                else
                {
                    Move.Flags |= ChessResultF.CastleK;
                    i += kCastleK.Length;
                    offsetX = ChessOffset.kCastleK;
                }

                Move.ToPos = new ChessPosition((byte)(ChessPosition.kXK + offsetX), color.RowKing);
            }
            else
            {
                int typeId = ChessType.GetTypeIdFrom(notation[i]);      // what type moved. // may return -1;
                if (typeId < 0) // bad type
                {
                    TypeId = ChessTypeId.Pawn; // assume its a pawn 
                }
                else
                {
                    TypeId = (ChessTypeId)typeId;
                    Debug.Assert(ChessType.IsValidId(TypeId));
                    i++;  // got type.
                }

                ChessPosition posUnk = ChessPosition.kNull;
                if (ChessPosition.IsValidNotation(notation, i))
                {
                    // might be From or To depending on what follows.
                    posUnk = new ChessPosition(notation[i], notation[i + 1]);
                    i += 2;
                }
                else if (ChessPosition.IsXFile(notation[i]))
                {
                    // partial From x = hxg3 or Rce7 
                    From = new ChessPosition(ChessPosition.GetX(notation[i]), ChessPosition.kDim);       // PARTIAL X
                    i++;
                }
                else if (ChessPosition.IsYRank(notation[i]))
                {
                    // partial From Y = R7g5
                    From = new ChessPosition(ChessPosition.kDim, ChessPosition.GetY(notation[i]));       // PARTIAL Y
                    i++;
                }

                if (i < notation.Length)
                {
                    if (notation[i] == 'x')
                    {
                        Move.Flags |= ChessResultF.Capture;
                        i++;
                    }
                    else if (notation[i] == '-')    // optional - ignore it.
                    {
                        i++;
                    }

                    if (ChessPosition.IsValidNotation(notation, i))
                    {
                        Move.ToPos = new ChessPosition(notation[i + 0], notation[i + 1]);
                        i += 2;
                    }
                }

                if (posUnk.IsOnBoard)  // decide what posUnk is used for.
                {
                    if (!Move.ToPos.IsOnBoard)   // Never allowed.
                    {
                        Move.ToPos = posUnk;
                    }
                    else
                    {
                        From = posUnk;
                    }
                }

                if (TypeId == ChessTypeId.Pawn
                    && !From.IsOnBoard
                    && !Move.Flags.IsAny(ChessResultF.Capture)
                    && color.GetRank(Move.ToPos) != 1 + ChessOffset.kPawnOpen)
                {
                    // In this case, I can predict where the pawn came From. 
                    From = new ChessPosition(Move.ToPos.X, (byte)(Move.ToPos.Y - color.DirY));
                }
            }

            // https://en.wikipedia.org/wiki/Chess_annotation_symbols
            // Suffixes.
            for (; i < notation.Length; i++)
            {
                char ch = notation[i];

                // Q (promoting to queen). Sometimes an equals sign or parentheses are used: e8=Q or e8(Q),
                if (IsPromoteChar(ch))
                {
                    Move.Flags |= GetPromoteFlag(ch);
                    continue;
                }

                switch (ch)
                {
                    case '=':
                        if (i + 1 < notation.Length && IsPromoteChar(notation[i + 1]))
                        {
                            Move.Flags |= GetPromoteFlag(notation[i + 1]);
                            i++;
                            continue;
                        }
                        break;
                    case '(':
                        if (i + 2 < notation.Length && IsPromoteChar(notation[i + 1]) && notation[i + 2] == ')')
                        {
                            Move.Flags |= GetPromoteFlag(notation[i + 1]);
                            i += 2;
                            continue;
                        }
                        break;
                    case '#':
                        Move.Flags |= ChessResultF.Check | ChessResultF.Checkmate;
                        continue;
                    case '+':
                        Move.Flags |= ChessResultF.Check;
                        if (i + 1 < notation.Length && notation[i + 1] == '+') // ++
                        {
                            Move.Flags |= ChessResultF.Checkmate;
                            i++;
                        }
                        continue;
                    case '?':
                        // a questionable/bad move.
                        Move.Flags |= ChessResultF.Bad;
                        continue;
                    case '!':
                        // an important/good move.
                        Move.Flags |= ChessResultF.Good;
                        continue;
                }
                break;  // nothing i recognize so stop.
            }

            return i;
        }

        bool IsMatch(ChessPiece piece, ChessColor color)
        {
            return piece != ChessPiece.kNull && piece.IsOnBoard && piece.IsMatch(TypeId, color);
        }

        public ChessPiece GetPiece(ChessGameBoard board, ChessRequestF flagsReq)
        {
            // Get the piece this notation references. Resolve vague reference based on valid moves for the current board state.

            if (ChessPiece.IsValidId(Move.Id))
            {
                // TypeId = piece1.TypeId; // assume already set.
                return board.GetPiece(Move.Id);
            }

            ChessColor color = board.State.TurnColor;
            if (From.IsOnBoard)  // I know where it was From.
            {
                ChessPiece piece1 = board.GetAt(From);
                if (IsMatch(piece1, color))
                {
                    Move.Id = piece1.Id;
                    // TypeId = piece1.TypeId; // assume already set.
                    return piece1;
                }
                ChessGame.InternalFailure("Notation GetPiece"); // weird? this should never happen!
            }

            // NOTE If the From field is not accurate (or partial, vague) we must try to guess which piece it is.
            // Find the piece with correct type that CAN move to 'To'.

            var possibles = new List<ChessPiece>();
            foreach (ChessPiece piece2 in board.Pieces)
            {
                if (!IsMatch(piece2, color))
                    continue;
                if (From.IsOnBoardX && piece2.Pos.X != From.X)   // Must be the Type in this x/file.
                    continue;
                if (From.IsOnBoardY && piece2.Pos.Y != From.Y)   // Must be the Type in this y/rank.
                    continue;

                // can this piece make this move?
                ChessResultF flags = board.TestMove(piece2, Move.ToPos, flagsReq | ChessRequestF.Test);
                if (flags.IsAllowedMove())
                {
                    possibles.Add(piece2);
                }
            }

            int countPossible = possibles.Count;
            if (countPossible > 1)
            {
                // Too vague. A check must invalidate some of these possible moves? Narrow the list.
                for (int i = 0; i < possibles.Count; i++)
                {
                    ChessResultF flags = board.Move(possibles[i], Move.ToPos, flagsReq | ChessRequestF.AssumeValid | ChessRequestF.Test);
                    if (!flags.IsAllowedMove())
                    {
                        possibles.RemoveAt(i); i--;
                    }
                }
            }

            if (possibles.Count == 1)
            {
                ChessPiece piece4 = possibles[0];
                Move.Id = piece4.Id;
                From = piece4.Pos;   // resolve From vagueness.
                return piece4;
            }

            ChessGame.InternalFailure("Vague Notation!");
            return ChessPiece.kNull;    // This is bad ! 2 or more pieces could move here !!! Idiot shorthand notation is too vague!
        }

        public static List<ChessNotation1> LoadPgn(string[] lines, ref int lineNumber)
        {
            // Load a list of notation lines. 2 moves per turn  .
            // https://thechessworld.com/articles/general-information/15-best-chess-games-of-all-time-annotated/
            // https://en.wikipedia.org/wiki/Portable_Game_Notation#:~:text=Portable%20Game%20Notation%20(PGN)%20is,supported%20by%20many%20chess%20programs.
            // https://www.pgnmentor.com/files.html

            var listMoves = new List<ChessNotation1>();

            for (; lineNumber < lines.Length; lineNumber++)
            {
                string line = lines[lineNumber];
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                char ch = line[0];
                if (ch == kCommentChar)   // skip line comments
                    continue;

                if (ch == '[')   // skip PGN sections.
                {
                    if (listMoves.Count > 0)
                        break;  // a header for another game. Stop.
                    continue;
                }

                int i = 0;
                while (i < line.Length)
                {
                    if (char.IsWhiteSpace(line[i]))
                    {
                        i++;
                        continue;
                    }

                    int turnNumber = ChessPlayState.GetTurnNumberForMove(listMoves.Count);
                    string turnPrefix = $"{turnNumber}."; // prefixed with turn number? ignore this.
                    if (string.Compare(line, i, turnPrefix, 0, turnPrefix.Length) == 0)
                    {
                        i += turnPrefix.Length;
                        continue;
                    }

                    if (line[i] == '{')
                    {
                        // skip {comments}.
                        i++;
                        int j = line.IndexOf('}', i);
                        if (j < 0)
                        {
                            ChessGame.InternalFailure("Unclosed comment {}");
                            return null;
                        }
                        i = j + 1;
                        continue;
                    }

                    var notation1 = new ChessNotation1();

                    int k = i;
                    i = notation1.SetNotation(line, i, ChessPlayState.GetColorForMove(listMoves.Count));
                    if (!notation1.IsValid)
                    {
                        ChessGame.InternalFailure("Load SetNotation");
                        return null;
                    }

                    listMoves.Add(notation1);
                }
            }

            return listMoves;
        }

        public ChessNotation1()
        {
            // Assume SetNotation() will be called.     
        }
    }

    /// <summary>
    /// ChessNotation1 with reversible StateString.
    /// </summary>
    public class ChessNotationRev : ChessNotation1
    {
        public readonly string StateString;       // allow role back to board state. the state BEFORE this move. GetStateString()

        public ChessNotationRev(string stateString)
        {
            StateString = stateString;
        }
    }
}
