using ChessChallenge.API;
using System;

namespace ChessChallenge.Example
{
    // A simple bot that can spot mate in one, and always captures the most valuable piece it can.
    // Plays randomly otherwise.
    public class EvilBot : IChessBot
    {
        // Piece values: null, pawn, knight, bishop, rook, queen, king
        int[] pieceValues = { 0, 100, 250, 300, 500, 900, 2000 };

        public Move Think(Board board, Timer timer)
        {
            var moveChoice = BestMove(board, 0);
            return moveChoice.Item1;
        }

        public Tuple<Move, int> BestMove(Board board, int depth)
        {
            Move[] allMoves = board.GetLegalMoves();
            if (allMoves.Length == 0)
            {
                return Tuple.Create(new Move(), 0);
            }

            // Pick a random move to play if nothing better is found
            Random rng = new();
            Move moveToPlay = allMoves[rng.Next(allMoves.Length)];
            int highestValueMove = 0;

            foreach (Move move in allMoves)
            {
                // Always play checkmate in one
                if (MoveIsCheckmate(board, move))
                {
                    highestValueMove += 10000;
                    moveToPlay = move;
                    break;
                }

                // Find info about pieces
                Piece movingPiece = board.GetPiece(move.StartSquare);
                Piece capturedPiece = board.GetPiece(move.TargetSquare);
                int movingPieceValue = pieceValues[(int)movingPiece.PieceType];
                int capturedPieceValue = pieceValues[(int)capturedPiece.PieceType];

                // Open with knights
                if (depth == 0 && board.PlyCount <= 3 && movingPiece.IsKnight && (move.TargetSquare.File == 2 || move.TargetSquare.File == 5))
                {
                    moveToPlay = move;
                    break;
                }

                // Start with captured value
                int moveValue = capturedPieceValue;

                // Push pawns in end game
                if (movingPiece.IsPawn && board.PlyCount >= 40)
                {
                    moveValue += 25;
                }

                // Try not to move high value pieces
                moveValue -= (movingPieceValue / 100);

                // Bias towards center Ranks and Files
                if (move.TargetSquare.Rank == 3 || move.TargetSquare.Rank == 4)
                {
                    moveValue += 5;
                }
                if (move.TargetSquare.File == 2 || move.TargetSquare.File == 5)
                {
                    moveValue += 5;
                }
                if (move.TargetSquare.File == 3 || move.TargetSquare.File == 4)
                {
                    moveValue += 10;
                }

                // Check, it might lead to mate
                if (MoveIsCheck(board, move))
                {
                    moveValue += 50;
                }

                // Castle
                if (move.IsCastles)
                {
                    moveValue += 150;
                }

                // Avoid draws
                if (MoveIsDraw(board, move))
                {
                    moveValue -= 10000;
                }

                // Avoid attacked squares
                if (board.SquareIsAttackedByOpponent(move.TargetSquare))
                {
                    moveValue -= movingPieceValue / 3;
                }

                // Move away from attacked squares
                if (board.SquareIsAttackedByOpponent(move.StartSquare))
                {
                    moveValue += movingPieceValue / 3;
                }

                // Promote to queen
                if ((int)move.PromotionPieceType == 5)
                {
                    moveValue += 1000;
                }

                // Consider the future
                if (depth == 0 || depth == 1)
                {
                    board.MakeMove(move);
                    moveValue -= (BestMove(board, depth + 1).Item2);
                    board.UndoMove(move);
                }

                // Use move if highest so far
                if (moveValue > highestValueMove)
                {
                    highestValueMove = moveValue;
                    moveToPlay = move;
                }
            }

            return Tuple.Create(moveToPlay, highestValueMove);
        }

        // Test if this move gives checkmate
        bool MoveIsDraw(Board board, Move move)
        {
            board.MakeMove(move);
            bool isDraw = board.IsDraw();
            board.UndoMove(move);
            return isDraw;
        }

        // Test if this move gives checkmate
        bool MoveIsCheck(Board board, Move move)
        {
            board.MakeMove(move);
            bool isCheck = board.IsInCheck();
            board.UndoMove(move);
            return isCheck;
        }

        // Test if this move gives checkmate
        bool MoveIsCheckmate(Board board, Move move)
        {
            board.MakeMove(move);
            bool isMate = board.IsInCheckmate();
            board.UndoMove(move);
            return isMate;
        }
    }
}