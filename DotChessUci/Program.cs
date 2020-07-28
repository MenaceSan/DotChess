using DotChess;
using System;

namespace DotChessUci
{
    class Program
    {
        static void Main(string[] args)
        {
            var uci = new ChessUci(Console.Out);
            while (uci.Command(Console.In.ReadLine()) != ChessUciRet.Quit)
            {
            }
        }
    }
}
