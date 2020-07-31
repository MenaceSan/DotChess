using DotChess;
using System;

namespace DotChessUci
{
    class Program
    {
        static void Main(string[] args)
        {
            // Load the UCI Interface and push console input to it.
            var uci = new ChessUci(Console.Out);
            while (uci.Command(Console.In.ReadLine()) != ChessUciRet.Quit)
            {
            }
        }
    }
}
