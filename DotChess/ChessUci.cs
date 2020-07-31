//  
// Copyright (c) 2020 Dennis Robinson (www.menasoft.com). All rights reserved.  
// Licensed under the MIT License. See ReadMe.md file in the project root for full license information.  
// 
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Mail;
using System.Threading;

namespace DotChess
{
    public enum ChessUciRet
    {
        // immediate result from UCI Command()
        UnkCmd,    // unknown command.
        UnkArg,     // invalid arg / syntax error on known command
        Err,      // error on command.
        Ok,     // Good
        Quit,   // Done.
    }

    /// <summary> 
    /// TODO: Drive the ChessGame 'engine' via standard UCI commands. 
    /// See "uci-engine-interface.txt" file.
    /// https://en.wikipedia.org/wiki/Universal_Chess_Interface
    /// https://stackoverflow.com/questions/17003561/using-the-universal-chess-interface
    /// http://wbec-ridderkerk.nl/html/UCIProtocol.html
    /// </summary>
    public class ChessUci
    {
        public readonly TextWriter Output;   // Write my output to here.
        public CancellationTokenSource Cancel;    // Cancel ponder action from here.
        public ManualResetEvent GoWorkDone;   // Set event when GoWork done.
        public ChessGame Game = new ChessGame();

        // State info.
        public bool Started;
        public bool DebugMode;
        public bool AnalyseMode;        // kOpt_AnalyseMode = "UCI_AnalyseMode"

        bool _ponder = false;
        int _wtime = 122000;
        int _btime = 120000;
        int _winc = 2000;
        int _binc = 2000;

        // uci_limitstrength
        // uci_elo

        public const string kNullMove = "0000"; // ?

        // Sent from engine back to GUI.
        public const string kOut_id = "id";
        public const string kOut_option = "option";        // declare i support some option.
        public const string kOut_uciok = "uciok";
        public const string kOut_readyok = "readyok";
        public const string kOut_info = "info";            // Send my testing info back to observer.
        public const string kOut_bestmove = "bestmove";        // final results. e.g. bestmove d7d5

        const string kTypeName = "name";

        const string kOpt_Hash = "Hash";
        const string kOpt_ClearHash = "Clear Hash"; // TODO Parse 2 words !!!
        const string kOpt_AnalyseMode = "UCI_AnalyseMode";

        public void WriteOutput(params string[] data)
        {
            // Push text to output. e.g. kOut_info
            lock (this) // thread safe.
            {
                Output?.WriteLine(string.Join(" ", data));
            }
        }

        void WriteUci()
        {
            WriteOutput(kOut_id, kTypeName, "DotChess");
            WriteOutput(kOut_id, "author", "https://www.menasoft.com/dotchess");
        }

        void Cmd_Uci()
        {
            // send once as a first command after program boot
            Started = true;
            WriteUci();

            // What "setoption" variables can be set?
            WriteOutput(kOut_option, kTypeName, kOpt_ClearHash, "type", "button");
            WriteOutput(kOut_option, kTypeName, kOpt_Hash, "type", "spin", "default 1 min 1 max 128");
            // WriteMessage(kOut_option, kTypeName, kOpt_AnalyseMode);

            WriteOutput(kOut_uciok);   // Done.
        }

        static bool GetBool(string s)
        {
            // like bool.TryParse(s, out b) // s = System.Boolean.FalseString;
            if (string.IsNullOrWhiteSpace(s))
                return false;
            return s[0] == 't' || s[0] == 'T' || s[0] == '1';
        }

        ChessUciRet Cmd_SetOption(string[] cmds, int i)
        {
            if (cmds.Length < i + 2)
                return ChessUciRet.Err;
            if (cmds[i] != kTypeName)
                return ChessUciRet.UnkArg;
            i++;
            switch (cmds[i])
            {
                case kOpt_ClearHash:    // TODO Parse 2 words !!!
                    return ChessUciRet.Ok;
                case kOpt_Hash:
                    return ChessUciRet.Ok;
                case kOpt_AnalyseMode:
                    AnalyseMode = false;
                    if (cmds.Length < i + 2)
                        return ChessUciRet.Ok;
                    AnalyseMode = GetBool(cmds[i + 2]);
                    return ChessUciRet.Ok;

                case "OwnBook":
                    break;
            }
            return ChessUciRet.UnkArg;   // ignored.
        }

        ChessUciRet Cmd_Position(string[] cmds, int i)
        {
            // set up the position described in fenstring 
            // GUI: tell the engine the position to search
            // e.g. "position startpos moves e2e4" or "position startpos moves e2e4 e7e5"

            if (cmds.Length < i + 2)
                return ChessUciRet.Err;

            if (cmds[i + 1] != "startpos")
            {
                Game.Board = new ChessGameBoard(cmds, i + 1, false);    // FEN for start
                i += ChessPlayState.kFenParams;
            }
            i += 2;

            for (; i < cmds.Length; i++)
            {
                string cmd = cmds[i];
                if (cmd == "moves") // ignore keyword.
                {
                    continue;
                }
                // Add the move.
                Game.Move(new ChessNotationPly(cmd, Game.TurnColor));
            }

            return ChessUciRet.Ok;
        }

        void GoWork()
        {
            // On a worker thread do what i need to do until done or Cancel.

            Game.TesterW.Cancel = Cancel.Token;
            ChessMoveId move = Game.RecommendBest1();

            // report bestmove.
            WriteOutput(kOut_bestmove, Game.GetNotation(move).ToString(), _ponder ? "ponder" : null, _ponder ? "" : null);
            _ponder = false;    // always turn this off.

            GoWorkDone.Set();   // done.
        }

        ChessUciRet Cmd_Go(string[] cmds, int i)
        {
            // tell the engine to start searching in this case give it the timing information in milliseconds
            // e.g. go wtime 122000 btime 120000 winc 2000 binc 2000

            if (cmds.Length < 2)
                return ChessUciRet.Ok;

            i++;
            for (; i < cmds.Length; i++)
            {
                string cmd = cmds[i];
                switch (cmd.ToLower())
                {
                    case "searchmoves":
                        // research just the selected moves.
                        Game.TesterW.Reset();
                        Game.TesterW.BestMoves = new List<ChessBestMoves>();
                        for (int j = i + 1; j < cmds.Length; j++)
                        {
                            string cmdMove = cmds[j];
                            var notation = new ChessNotationPly();
                            int k = notation.SetNotation(cmdMove, 0, Game.TurnColor);
                            if (k <= 0)
                                break;
                            Game.TesterW.BestMoves.Add(new ChessBestMoves(notation.Move));
                        }
                        break;
                    case "ponder":      // its not my turn, but try to figure out how to respond to the possible moves of my opponent.
                        _ponder = GetBool(cmds[++i]);
                        break;
                    case "infinite": // go til stop.  
                        break;
                    case "wtime": //  
                        if (!int.TryParse(cmds[++i], out _wtime))
                            return ChessUciRet.Err;
                        break;
                    case "btime": // 
                        if (!int.TryParse(cmds[++i], out _btime))
                            return ChessUciRet.Err;
                        break;
                    case "winc": // 
                        if (!int.TryParse(cmds[++i], out _winc))
                            return ChessUciRet.Err;
                        break;
                    case "binc": // 
                        if (!int.TryParse(cmds[++i], out _binc))
                            return ChessUciRet.Err;
                        break;

                    case "movestogo":
                        break;
                    case "depth":       // search x plies only.
                        break;
                    case "nodes":       // search x nodes only,
                        break;
                    case "mate":    // search for a mate in x moves
                        break;
                    case "movetime":        // search exactly x mseconds
                        break;
                }
            }

            // Do work on back thread.
            Cancel = new CancellationTokenSource();
            GoWorkDone = new ManualResetEvent(false);
            if (!ThreadPool.QueueUserWorkItem(x => ((ChessUci)x).GoWork(), this))
            {
                Cancel = null;  // should never happen!
                return ChessUciRet.Err;
            }

            return ChessUciRet.Ok;
        }

        ChessUciRet Cmd_Stop()
        {
            if (Cancel == null)     // was not pondering.
                return ChessUciRet.UnkArg;
            Cancel.Cancel();
            GoWorkDone.WaitOne(); // Wait for thread join/stop.
            Cancel = null;  // done.
            return ChessUciRet.Ok;
        }

        public ChessUciRet Command(string[] cmds, int i)
        {
            if (cmds.Length <= i)
                return ChessUciRet.UnkCmd;       // blank = ignore it.

            string cmd0 = cmds[i].ToLower();
            i++;
            string cmd1 = (cmds.Length > i) ? cmds[i].ToLower() : null;

            switch (cmd0)
            {
                case "?":
                case "h":
                case "help": // Not official UCI "help" command.
                    WriteUci();
                    return ChessUciRet.Ok;

                case "quit":
                case "q":
                    // exit the UCI app process.
                    return ChessUciRet.Quit;

                case "uci":     // start UCI mode.
                    Cmd_Uci();
                    return ChessUciRet.Ok;

                case "debug":   // [ on | off ]
                    DebugMode = (cmd1 == "on");
                    return ChessUciRet.Ok;

                case "isready":  // this is used to synchronize the engine with the GUI
                    WriteOutput(kOut_readyok); // we are good.
                    return ChessUciRet.Ok;

                case "setoption":  // setoption name  [value ]  // setoption name Hash value 128
                    return Cmd_SetOption(cmds, i);

                case "register":    // ignore this since no registration is required.
                    return ChessUciRet.Ok;

                case "ucinewgame":
                    // let the engine know if starting a new game
                    Game.ResetGame();
                    return ChessUciRet.Ok;

                case "position":
                    return Cmd_Position(cmds, i);

                case "go":
                    return Cmd_Go(cmds, i);

                case "stop":
                    return Cmd_Stop();

                case "ponderhit":
                    // the user has played the expected move. This will be sent if the engine was told to ponder on the same move the user has played. 
                    // The engine should continue searching but switch from pondering to normal search.
                    _ponder = false;

                    return ChessUciRet.Ok;

                default:
                    return ChessUciRet.UnkCmd;   // ignore it.
            }
        }

        public ChessUciRet Command(string cmd)
        {
            // interpret the incoming UCI command.
            // return ChessUciRet.Unk = Always ignore stuff i don't understand.
            // NOTE: Do not block this thread with ponder. Must receive the "stop" command async.
            // https://gist.github.com/aliostad/f4470274f39d29b788c1b09519e67372

            if (cmd == null)
                return ChessUciRet.Quit;      // ^c is same as quit
            if (string.IsNullOrWhiteSpace(cmd))
                return ChessUciRet.UnkCmd;       // blank = ignore it.
            string[] cmds = cmd.Split();

            for (int i = 0; i < cmds.Length; i++)
            {
                try
                {
                    ChessUciRet ret = Command(cmds, i);
                    if (ret != ChessUciRet.UnkCmd)
                        return ret;
                }
                catch
                {
                    return ChessUciRet.Err;
                }
            }

            return ChessUciRet.UnkCmd;
        }

        public ChessUci(TextWriter output)
        {
            Output = output;
            Game.TesterW = new ChessBestTester(Game.Board, 1, new Random(), CancellationToken.None);
        }
    }
}
