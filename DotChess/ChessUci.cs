//  
// Copyright (c) 2020 Dennis Robinson (www.menasoft.com). All rights reserved.  
// Licensed under the MIT License. See ReadMe.md file in the project root for full license information.  
// 
using System.IO;
using System.Threading;

namespace DotChess
{
    /// <summary> 
    /// TODO Drive the ChessGame 'engine' via standard UCI commands. 
    /// See "uci-engine-interface.txt" file.
    /// </summary>
    public class ChessUci
    {
        public readonly TextWriter Output;   // Write my output to here.
        public ChessGame Game;
        public CancellationTokenSource Cancel;    // Cancel ponder action from here.

        public void Command(string cmd)
        {

        }

        public ChessUci(TextWriter output)
        {
            Output = output;
        }

    }
}
