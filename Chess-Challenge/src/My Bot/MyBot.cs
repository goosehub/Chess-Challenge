using ChessChallenge.API;
using System;
using System.Collections.Generic;
using System.Linq;

public class MyBot : IChessBot
{
    // Piece values: null, pawn, knight, bishop, rook, queen, king
    int[] pieceValues = { 0, 100, 410, 420, 630, 1200, 2000 };
    int maxDepth = 2;

    public Move Think(Board board, Timer timer)
    {
        var moveChoice = BestMove(board, timer, 0);
        return moveChoice.Item1;
    }

    public Tuple<Move, int> BestMove(Board board, Timer timer, int depth)
    {
        // Prep for loop
        Move[] allMoves = board.GetLegalMoves();
        if (allMoves.Length == 0)
        {
            return Tuple.Create(new Move(), 0);
        }
        Move moveToPlay = allMoves[0];
        PieceList[] pieces = board.GetAllPieceLists();
        int highestValueMove = -10000;
        int whiteValue = (pieces[0].Count * pieceEstimate(1)) + (pieces[1].Count * pieceEstimate(2)) + (pieces[2].Count * pieceEstimate(3)) + (pieces[3].Count * pieceEstimate(4));
        int blackValue = (pieces[4].Count * pieceEstimate(1)) + (pieces[5].Count * pieceEstimate(2)) + (pieces[6].Count * pieceEstimate(3)) + (pieces[7].Count * pieceEstimate(4));
        bool winning = whiteValue > blackValue && board.IsWhiteToMove || whiteValue < blackValue && !board.IsWhiteToMove ? true : false;
        int depthValue = 0;

        foreach (Move move in allMoves)
        {
            // Always play checkmate in one
            if (MoveIsCheckmate(board, move) && depth <= 1)
            {
                highestValueMove += 10000;
                moveToPlay = move;
                System.Diagnostics.Debug.WriteLine("Checkmate found at depth: " + depth);
                break;
            }

            // Get info
            Random rng = new();
            int moveValue = rng.Next(3); // RNG for fresh games
            Piece movingPiece = board.GetPiece(move.StartSquare);
            Piece capturedPiece = board.GetPiece(move.TargetSquare);
            int movingPieceValue = pieceEstimate((int)movingPiece.PieceType, movingPiece);
            int capturedPieceValue = pieceEstimate((int)capturedPiece.PieceType, movingPiece);

            // Open with knights with reasonable
            if (depth == 0 && board.PlyCount <= 3 && movingPiece.IsKnight && (move.TargetSquare.File == 2 || move.TargetSquare.File == 5))
            {
                moveValue += 200;
            }

            // Start with captured value
            moveValue += capturedPieceValue;

            // Encourage capture if winning
            if (winning)
            {
                moveValue += (capturedPieceValue / 50);
            }

            // Avoid draws if winning
            if (winning && MoveIsDraw(board, move))
            {
                moveValue -= 10000;
            }

            // Push pawns in end game
            if (movingPiece.IsPawn && board.PlyCount >= 40)
            {
                moveValue += 25;
            }

            // Try not to move high value pieces
            moveValue -= (movingPieceValue / 100);

            // Bias towards center Ranks and Files
            if (move.TargetSquare.Rank == 3 || move.TargetSquare.Rank == 4 || move.TargetSquare.File == 2 || move.TargetSquare.File == 5)
            {
                moveValue += 5;
            }
            if (move.TargetSquare.File == 3 || move.TargetSquare.File == 4)
            {
                moveValue += 10;
            }

            // Check, it might lead to mate, less so in end game
            if (MoveIsCheck(board, move))
            {
                moveValue += board.PlyCount <= 30 ? 40 : 20;
            }

            // Castle
            if (move.IsCastles)
            {
                moveValue += 50;
            }

            // Promote to queen
            if ((int)move.PromotionPieceType == 5)
            {
                moveValue += 1000;
            }

            // Lazy depth check by avoiding staying on or targeting attacked squares at end of depth
            if (board.SquareIsAttackedByOpponent(move.TargetSquare) || board.SquareIsAttackedByOpponent(move.StartSquare))
            {
                moveValue -= movingPieceValue;
            }

            // If the move is promising and time permits, consider the future carefully
            depthValue = 0;
            if (depth <= maxDepth && (depth != maxDepth ||
                    (timer.MillisecondsRemaining > 10 * 1000 && moveValue >= highestValueMove)
                ))
            {
                // Undo lazy depth check
                if (board.SquareIsAttackedByOpponent(move.TargetSquare) || board.SquareIsAttackedByOpponent(move.StartSquare))
                {
                    moveValue += movingPieceValue;
                }
                board.MakeMove(move);
                depthValue = BestMove(board, timer, depth + 1).Item2 * -1;
                moveValue += depthValue;
                board.UndoMove(move);
            }

            // Use move if highest so far
            if (moveValue > highestValueMove)
            {
                highestValueMove = moveValue;
                moveToPlay = move;
            }
        }

        // Debug
        if (true && depth == 0)
        {
            System.Diagnostics.Debug.WriteLine(moveToPlay.ToString() + "-" + highestValueMove + " | Depth Value: " + depthValue + " | Winning: " + winning);
        }

        return Tuple.Create(moveToPlay, highestValueMove);
    }

    // Estimate value of a piece
    int pieceEstimate(int pieceIndex, Piece piece = new Piece())
    {
        int pieceValue = pieceValues[pieceIndex];
        if (!piece.IsNull)
        {
            // Pawns close to promotion are more valuable
            if (piece.IsPawn && ((piece.IsWhite && piece.Square.Rank == 5) || (!piece.IsWhite && piece.Square.Rank == 2)))
            {
                pieceValue += 100;
            }
            if (piece.IsPawn && ((piece.IsWhite && piece.Square.Rank == 6) || (!piece.IsWhite && piece.Square.Rank == 1)))
            {
                pieceValue += 400;
            }
        }
        return pieceValue;
    }

    // Test if this move gives draw
    bool MoveIsDraw(Board board, Move move)
    {
        board.MakeMove(move);
        bool isDraw = board.IsDraw();
        board.UndoMove(move);
        return isDraw;
    }

    // Test if this move gives check
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