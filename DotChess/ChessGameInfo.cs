//  
// Copyright (c) 2020 Dennis Robinson (www.menasoft.com). All rights reserved.  
// Licensed under the MIT License. See ReadMe.md file in the project root for full license information.  
// 
using System;
using System.Diagnostics;
using System.Net.Mail;

namespace DotChess
{
    /// <summary>
    /// The ChessGameInfo for a single player/color. PGN header data.
    /// </summary>
    [Serializable]
    public class ChessGamePlayer
    {
        public string Name;     // color/side name. key=White/Black
        public int Elo;         // key=WhiteElo/BlackElo
    }

    /// <summary>
    /// The status of a playing or completed historical game. PGN header data.
    /// https://en.wikipedia.org/wiki/Portable_Game_Notation
    /// https://en.wikipedia.org/wiki/Portable_Game_Notation#Tag_pairs
    /// </summary>
    [Serializable]
    public class ChessGameInfo
    {
        public string Event;    // Name of the event.
        public string Site;    // Name of the location.
        public int Round;       // Game number in a series of games.

        public ChessGamePlayer White;    // color/side info
        public ChessGamePlayer Black;    // color/side info

        public DateTime FirstMove;          // when did game start? AKA "Date" + Time.
        public string Result;   // Result text from PGN file. e.g. ChessNotation1.kStalemate, etc.
        public ChessColorId ResultId;   // Interpretation of Result.

        public string ECO;      // Encyclopedia of Chess Openings  e.g. "A05 Reti opening"

        public DateTime Date => FirstMove.Date;     // In 2020/06/22 with ?? in case of unknown. (NO time)
        public TimeSpan Time => FirstMove.TimeOfDay;     // In 2020/06/22 with ?? in case of unknown. (NO time)

        public int WhiteElo => White.Elo;
        public int BlackElo => Black.Elo;

        // TODO Time limit. Auto resign when time passed.
        // TODO email when its your turn ?

        public ChessColor ColorWinner => ChessColor.GetColor(ResultId);

        public override int GetHashCode()
        {
            // a unique id for this game info.
            int hashCode = Event.GetHashCode();
            hashCode ^= Site.GetHashCode();
            hashCode ^= Round.GetHashCode();
            hashCode ^= White.Name.GetHashCode();
            hashCode ^= Black.Name.GetHashCode();
            hashCode ^= Date.GetHashCode();     // Historical game.
            hashCode ^= ResultId.GetHashCode();
            return hashCode;
        }

        public void Reset()
        {
            FirstMove = DateTime.MinValue;  // started when?
        }

        public ChessGameInfo()
        {
            White = new ChessGamePlayer { Name = ChessColor.kWhite.ToString() };    // name
            Black = new ChessGamePlayer { Name = ChessColor.kBlack.ToString() };    // name
            Reset();
        }

        public ChessGamePlayer GetPlayer(ChessColor color)
        {
            return color == ChessColor.kWhite ? White : Black;
        }

        internal void MoveAdvance()
        {
            // Active Game. The current move is complete. Advance Turn.
 
            if (FirstMove == DateTime.MinValue)
                FirstMove = DateTime.UtcNow;
        }

        public int LoadPgn(string[] lines, int lineNumber = 0)
        {
            // load game info from a PGN file.

            for (; lineNumber < lines.Length; lineNumber++)
            {
                string line = lines[lineNumber];
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                if (line[0] == ChessNotationPly.kCommentChar)     // always ignore line comments.
                    continue;

                if (line[0] != '[')
                {
                    break;
                }

                // Parse Game header entry.
                int j = line.IndexOf(' ', 1);
                if (j < 0)
                    continue;   // junk ? ignore it.

                string name = line.Substring(1, j - 1);

                j = line.IndexOf('"', j + 1);
                if (j < 0)
                    continue;   // junk ? ignore it.
                j++;
                int k = line.IndexOf('"', j);
                if (k < 0)
                    continue;   // junk ? ignore it.

                string value = line.Substring(j, k - j);
                if (string.IsNullOrWhiteSpace(name))
                    continue;
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                switch (name)
                {
                    case nameof(Event):
                        Event = value;
                        break;
                    case nameof(Site):
                        Site = value;
                        break;
                    case nameof(Round):
                        int.TryParse(value, out Round);
                        break;
                    case nameof(Black):
                        Black.Name = value;
                        break;
                    case nameof(White):
                        White.Name = value;
                        break;
                    case nameof(BlackElo):
                        int.TryParse(value, out Black.Elo);
                        break;
                    case nameof(WhiteElo):
                        int.TryParse(value, out White.Elo);
                        break;
                    case nameof(Date): // Read the odd date format.
                        // FirstMove
                        break;
                    case nameof(Time): // Time the game started, in HH:MM:SS format, in local clock time.
                        // FirstMove
                        break;
                        
                    case nameof(Result):
                        Result = value;
                        switch (value)
                        {
                            case ChessNotationPly.kWinWhite:
                                ResultId = ChessColorId.White;
                                break;
                            case ChessNotationPly.kWinBlack:
                                ResultId = ChessColorId.Black;
                                break;
                            case ChessNotationPly.kStalemate:
                                ResultId = ChessColorId.Stalemate;
                                break;
                            case ChessNotationPly.kActive:
                            default:
                                ResultId = ChessColorId.Undefined;
                                break;
                        }
                        break;
                    case nameof(ECO):
                        ECO = value;
                        break;

                    case "TimeControl":     // e.g. 40/7200:3600 (moves per seconds: sudden death seconds)
                    case "Annotator":        //  The person providing notes to the game.
                    case "PlyCount":        // total number of half-moves played.
                    case "Termination":     // Gives more details about the termination of the game. It may be abandoned, adjudication (result determined by third-party adjudication), death, emergency, normal, rules infraction, time forfeit, or unterminated.
                    case "Mode":            // OTB (over-the-board) ICS (Internet Chess Server)
                    case "FEN":         // The initial position of the chess board, in Forsyth-Edwards Notation. Use with SetUp=1.

                    case "SetUp":       // Used with FEN (1)
                        break;

                }
            }

            return lineNumber;
        }
    }
}
