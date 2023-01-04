//  
// Copyright (c) 2020 Dennis Robinson (www.menasoft.com). All rights reserved.  
// Licensed under the MIT License. See ReadMe.md file in the project root for full license information.  
// 
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace DotChess
{
    /// <summary>
    /// my calculation of score for the next move for a color given a board state.
    /// Predict, Suggest, Recommend, Evaluate best moves.
    /// </summary>
    public class ChessBestMove : ChessMoveId
    {
        public int Score;   // My calculated score for what may happen after this + n levels.

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int Compare2(ChessBestMove a, ChessBestMove b)
        {
            // For sorting in a list. 
            int diff = a.Score - b.Score;
            if (diff != 0)
                return diff;
            return ChessMoveId.Compare2(a, b);  // if they are equal score then compare other fields.
        }

        public ChessBestMove(ChessMove clone, ChessPieceId id)
            : base(clone, id)
        {
            // Score = unknown.
        }
    }

    /// <summary>
    /// Hold <= ChildMovesToKeep of my child moves for future query
    /// cache/save some of these. 1 in ChildMovesToKeep of the data can be re-used, next turn.
    /// Don't bother storing junk moves.
    /// </summary>
    public class ChessBestMoves : ChessBestMove
    {
        public List<ChessBestMoves> ChildMoves; // (if i have them) ChildMovesToKeep
        // Completed ? The ponder may have been interrupted.

        public ChessBestMoves(ChessMove clone, ChessPieceId id, List<ChessBestMoves> childMoves = null)
            : base(clone, id)
        {
            this.ChildMoves = childMoves;
        }
        public ChessBestMoves(ChessMoveId clone, List<ChessBestMoves> childMoves = null)
             : base(clone, clone.Id)
        {
            this.ChildMoves = childMoves;
        }
    }

    /// <summary>
    /// Score all possible (1 step next single) moves from this ChessBoard state.
    /// On a testing move, I should descend Depth levels and keep testing possible future moves.
    /// pick that best scoring path at each descent level.
    /// This is CPU bound so async will not help us. only hard threads/cores help.
    /// Must clone this if we use another thread.
    /// </summary>
    public class ChessBestTester
    {
        private readonly ChessGameBoard Board;   // Must be a clone to work on another thread.
        public readonly Random Random;     // Add a small random element for scoring otherwise equal moves. 0.01
        public readonly TextWriter UciOutput;   // Write my UCI "info" output to here.

        public CancellationToken Cancel = CancellationToken.None;       // Allow time based cancel of search.
        public bool AutoPonder;      // TODO When not my turn, Keep thinking until Cancel.

        public readonly int DepthMaxTarget;      // How many levels deep should i go ? How smart am i ?
        public int DepthMaxTurn;      // How many levels deep should i go in this turn?
        public int DepthParallel = 255;     // How deep should i allow Parallel usage? For testing. 255 = always allow Parallel.
        public int DepthCur;    // How many levels deep have i gone?
        public int ChildMovesToKeep = 5;       // Keep (at most) X best scoring moves. TODO Allow this to change ?

        public List<ChessBestMoves> BestMoves;   // All possible next (1) moves from Board, sorted by Score.
        public int TestCount;       // How many child tests.

        public int Score;    // the current BEST score at DepthCur level for all BestMoves.

        static int _ThreadsRunning = 0;         // < ChessUtil.kThreadsMax

        public int MoveCount => Board.State.MoveCount;  // helper.
        public ChessColor TurnColor => Board.State.TurnColor; // helper.

        public ChessBestTester(ChessGameBoard board, int depthMax, Random random, CancellationToken cancel)
        {
            this.Board = board;
            this.DepthMaxTarget = DepthMaxTurn = depthMax;   // Max depth for this. how hard will i think about it.
            this.Random = random;
            this.Cancel = cancel;   // CancellationToken.None
        }

        /// <summary>
        /// Flush all my remembered moves.
        /// </summary>
        public void Reset()
        {
            BestMoves = null;
        }

        private void StopPonder()
        {
            // If I am pondering while waiting. I must stop pondering so i can play.
            // Set Cancel flag and wait for threads to return.

            if (DepthCur <= 0)    // we are not pondering.
                return;

            // Cancel and wait for completion.
            // TODO
        }

        private void StartAutoPonder()
        {
            // Automatically Ponder on a background thread. (if its not my turn)
            // Do not halt this thread.
            // Look for moves the opponent might make and look for counters to it. 
            // assume most of the moves will be wasted. Since the opponent wont pick them.

            if (!AutoPonder)
                return;

            // TODO
        }

        /// <summary>
        /// On next move, keep BestMoves updated. Did i take any of the recommended/expected moves?
        /// </summary>
        /// <param name="piece">what piece moved?</param>
        /// <param name="posNew">where did it move to?</param>
        public void MoveNext(ChessPieceId id, ChessPosition posNew, bool isMyMove)
        {
            if (BestMoves == null)
                return;
            StopPonder();
            foreach (ChessBestMoves move2 in BestMoves)
            {
                if (move2.Id == id && move2.ToPos.Equals(posNew))
                {
                    BestMoves = move2.ChildMoves;
                    return;
                }
            }
            Reset();  // invalidated. They didn't take a move i tested, expected (or kept).
            if (!isMyMove)
            {
                StartAutoPonder();
            }
        }

        /// <summary>
        /// We backed up a move. Adjust to tester to account for this.
        /// </summary>
        /// <param name="movePrev"></param>
        public void MovePrev(ChessMoveId movePrev)
        {
            if (BestMoves == null)
                return;
            StopPonder();
            BestMoves = new List<ChessBestMoves> { new ChessBestMoves(movePrev, BestMoves) };
            StartAutoPonder();
        }

        /// <summary>
        /// IComparer for sorting best to worst scores.
        /// </summary>
        private class CompSortW : IComparer<ChessBestMove>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Compare(ChessBestMove a, ChessBestMove b)
            {
                return -ChessBestMove.Compare2(a, b);
            }
        }
        private static readonly CompSortW _CompSortW = new CompSortW();
        private class CompSortB : IComparer<ChessBestMove>
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public int Compare(ChessBestMove a, ChessBestMove b)
            {
                return ChessBestMove.Compare2(a, b);
            }
        }
        private static readonly CompSortB _CompSortB = new CompSortB();

        private void SortBest(int dir)
        {
            if (BestMoves.Count <= 1)
                return;
            if (dir > 0)
                BestMoves.Sort(_CompSortW);
            else
                BestMoves.Sort(_CompSortB);
        }

        private bool IsDepthStop()
        {
            // Stop descending? futile path? true = Don't descend any farther.
            // Higher scoring moves deserve more looking.

            if (Cancel.IsCancellationRequested)     // we are told to stop.
                return true;
            if (this.DepthCur >= DepthMaxTurn)    // we descended far enough.
            {
                // half life random depth?
                if (this.DepthCur >= DepthMaxTurn * 2)    // we descended far enough. Hard stop.
                    return true;
                int randVal = Random.Next(4);   // take a random chance to peek ahead another level.
                if (randVal == 0)
                    return true;
            }

            // trim futile test paths after X moves.
            bool futility = Score <= 2 && DepthCur >= 6;
            futility |= Score <= 1 && DepthCur >= 5;
            futility |= Score <= 0 && DepthCur >= 4;
            futility |= Score < 0 && DepthCur >= 3;
            if (futility)
            {
                // Score -= scoreDir * ChessType.kValueTick;  // minor bad effect for me.
                return true;
            }

            return false; // keep descending.
        }

        internal void TestPossibleNext(ChessPiece pieceMoved, ChessResultF newFlags, int scoreChange)
        {
            // Get Best score for all child moves past here.
            // Called by Move(ChessRequestF.Test)
            // Score this move + all possible next moves and how they might help or damage me.

            if (!newFlags.IsAllowedMove())  // last move didn't work!
            {
                // This should NEVER happen.
                ChessGame.InternalFailure("TestPossibleNext IsAllowedMove");
                return;
            }

            ChessColor color = pieceMoved.Color;
            int scoreDir = color.ScoreDir;     // how is this scored. Each color will maximize their own advantage.
            Score += scoreChange * scoreDir;

            if (newFlags.IsAny(ChessResultF.Checkmate))
            {
                Score += scoreDir * ChessType.kValueKing;
                return;
            }

            // Give a little more value for check. even though i don't really know how it will end up.
            if (newFlags.IsAny(ChessResultF.Check))
            {
                Score += scoreDir * ChessType.kValueTick;   // minor good effect
            }

            // Descend into next set of moves?
            if (IsDepthStop())
            {
                return;
            }

            Board.State.MoveCount++;    // next move for Opposite color
            DepthCur++;

            FindBestMoves2(newFlags.GetReqInCheck()); // update BestMoves for Opposite color

            // Distill best score from BestMoves
            // Q: Dilute the value of any move based on the number of alternate moves the opponent could take to avoid it. good (for me) and bad (for them).
            // or assume opponent will always take their best move ?
            // Assume color will maximize whats best for themselves. use * scoreDir.
            // Opposite color score is bad for original calling color. 

            if (BestMoves.Count > 0)
            {
                scoreDir = -scoreDir;   // reverse score direction for check opposite color.
                SortBest(scoreDir);
                Score = BestMoves[0].Score * scoreDir;  // Child Score incorporates current Score.

                // Drop moves we don't think we will use. > ChildMovesToKeep
                if (BestMoves.Count > ChildMovesToKeep)
                {
                    BestMoves.RemoveRange(ChildMovesToKeep, BestMoves.Count - ChildMovesToKeep);
                }
            }
            else
            {
                // No moves available means ChessResultF.Stalemate. Assume NOT ChessResultF.Checkmate for the caller! We would have seen this earlier.
            }

            // restore previous level state.
            DepthCur--;
            Board.State.MoveCount--;
        }

        private void InitDepthMax()
        {
            // optimize depth max.             
            Debug.Assert(DepthCur == 0);
            int moveCount = MoveCount;
            DepthMaxTurn = DepthMaxTarget;

            if (moveCount < 5 && Board.Score == 0 && DepthMaxTurn > 3)
                DepthMaxTurn = 3;
            if (moveCount < 20 && Board.CaptureCount == 0 && DepthMaxTurn > 3)
                DepthMaxTurn = 3;
        }

        private void UpdateBestScores(ChessRequestF flagsReq)
        {
            // descend into BestMoves and get scores. Score each move to find the best.

            var prevScore = this.Score;
            var prevBestMoves = this.BestMoves;

            foreach (ChessBestMoves move in prevBestMoves)
            {
                // Make the move and possibly descend into child moves. ASSUME Board.Move() will call TestPossibleNext()
                TestCount++;
                BestMoves = move.ChildMoves;    // Use ChildMoves we got from a previous test? If Any.
                // Next call will set Score and change BestMoves.
                ChessPiece pieceToMove = Board.GetPiece(move.Id);
                // ChessPosition posPrev = pieceToMove.Pos;
                ChessResultF flags = Board.Move(pieceToMove, move.ToPos, flagsReq | ChessRequestF.AssumeValid | ChessRequestF.Test, this); // ASSUME it calls TestPossibleNext()
                Debug.Assert(flags.IsAllowedMove());
                // Debug.Assert(pieceToMove.Pos.Equals(posPrev));      // No real move occurred. This should ALWAYS be true.

                if (DepthCur == 0 && UciOutput != null)
                {
                    // TODO // Display what i think of this move.
                    UciOutput.WriteLine(ChessUci.kOut_info);
                }

                move.Score = this.Score;    // record the score.
                move.ChildMoves = this.BestMoves;
                this.Score = prevScore;
            }

            BestMoves = prevBestMoves;  // Restore this to proper Depth.
        }

        private void FindBestMoves2(ChessRequestF flagsReq)
        {
            ChessColor color = TurnColor;   // whose turn to move?

            if (BestMoves == null) // If i haven't already been here.
            {
                // Init BestMoves with all possible moves. Scores added later. Trim futile moves.

                BestMoves = new List<ChessBestMoves>();   // All possible moves for this board and color at this board state.

                // find all valid moves from this state.
                foreach (ChessPiece piece in Board.Pieces)
                {
                    if (!piece.IsOnBoard || piece.Color != color)
                        continue;
                    List<ChessMove> moves = Board.GetValidMovesFor(piece, flagsReq);
                    foreach (var move in moves)
                    {
                        Debug.Assert(!piece.Pos.Equals(move.ToPos));
                        BestMoves.Add(new ChessBestMoves(move, piece.Id, null));
                    }
                }

                // TODO randomize the order of this in case Cancel is called.
            }

            if (BestMoves.Count <= 0)   // game over. Stalemate.
                return;

            // Score all valid moves to find which is best.
            int threadsAvail = ChessUtil.kThreadsMax - _ThreadsRunning;
            if (BestMoves.Count > 1 && threadsAvail > 1 && DepthCur < DepthParallel)
            {
                // sub-divide into chunks to run in Parallel.
                int j = 0;
                threadsAvail = Math.Min(threadsAvail, BestMoves.Count);
                var threads = new ChessBestTester[threadsAvail];
                for (int i = 0; i < threadsAvail; i++)
                {
                    int k = (BestMoves.Count * (i + 1)) / threadsAvail;
                    Debug.Assert(k > j);
                    threads[i] = new ChessBestTester(new ChessGameBoard(Board), DepthMaxTurn, Random, Cancel) { DepthCur = this.DepthCur, BestMoves = this.BestMoves.GetRange(j, k - j) };
                    j = k;
                }

                Interlocked.Add(ref _ThreadsRunning, threadsAvail);
                if (Cancel != CancellationToken.None)
                {
                    var opts = new ParallelOptions { CancellationToken = Cancel };
                    Parallel.ForEach(threads, opts, x => x.UpdateBestScores(flagsReq));
                }
                else
                {
                    Parallel.ForEach(threads, x => x.UpdateBestScores(flagsReq));
                }
                Interlocked.Add(ref _ThreadsRunning, -threadsAvail);

                // re-combine results.
                foreach (var test in threads)
                    TestCount += test.TestCount;
                SortBest(color.ScoreDir);
            }
            else
            {
                UpdateBestScores(flagsReq);
            }
        }

        /// <summary>
        /// Find the best scoring move for the given board and TurnColor. 
        /// NOTE: This can be VERY slow.
        /// </summary>
        /// <param name="flagsReq"></param>
        public void FindBestMoves(ChessRequestF flagsReq)
        {
            StopPonder();
            InitDepthMax();
            FindBestMoves2(flagsReq); // update scores for BestMoves. This can be VERY slow.
            StartAutoPonder();
        }

        public int GetBestMovesTieCount()
        {
            // How many moves are tied for best? So we can pick one randomly.
            // If we have multiple moves with the same score then pick one randomly.
            // ASSUME BestMoves sorted. SortBest() called.

            int countMoves = BestMoves.Count;
            if (countMoves <= 1)
                return countMoves;    // 0 = Game over. I have no moves. Checkmate or Stalemate.

            int i = 1;
            int scoreBest = BestMoves[0].Score;

            for (; i < countMoves && BestMoves[i].Score == scoreBest; i++)
            {
            }

            return i;
        }
    }
}
