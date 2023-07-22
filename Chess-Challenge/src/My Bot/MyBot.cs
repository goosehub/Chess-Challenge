using ChessChallenge.API;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;

public class MyBot : IChessBot
{
    // Piece values: null, pawn, knight, bishop, rook, queen, king
    int[] pieceValues = { 0, 100, 410, 420, 630, 1200, 5000 };
    int maxDepth = 2;
    // Inline variables
    // 100 = one normal pawn of value
    // 40 = Move end game starts

    public Move Think(Board board, Timer timer)
    {
        var moveChoice = BestMove(board, timer, 0);
        return moveChoice.Item1;
    }

    public Tuple<Move, int, int> BestMove(Board board, Timer timer, int depth)
    {
        // Get data and prep for loop
        Move[] allMoves = board.GetLegalMoves();
        if (allMoves.Length == 0)
        {
            return Tuple.Create(new Move(), 0, 0);
        }
        Move moveToPlay = allMoves[0];
        PieceList[] pieces = board.GetAllPieceLists();
        int highestValueMove = -10000;
        int depthValue = 0;

        // Eval
        int whiteValue = (pieces[0].Count * pieceEstimate(1)) + (pieces[1].Count * pieceEstimate(2)) + (pieces[2].Count * pieceEstimate(3)) + (pieces[3].Count * pieceEstimate(4)) + (pieces[4].Count * pieceEstimate(5));
        int blackValue = (pieces[6].Count * pieceEstimate(1)) + (pieces[7].Count * pieceEstimate(2)) + (pieces[8].Count * pieceEstimate(3)) + (pieces[9].Count * pieceEstimate(4)) + (pieces[10].Count * pieceEstimate(5));
        int evaluation = whiteValue - blackValue;
        bool losing = (board.IsWhiteToMove && evaluation < -100) || (!board.IsWhiteToMove && evaluation > 100) ? true : false;

        // Sort moves for best candidates first
        Move[] sortedMoves = allMoves.OrderByDescending(thisMove => rankMoveForSorting(board, thisMove)).ToArray();

        foreach (Move move in sortedMoves)
        {

            // Get data
            Random rng = new();
            int moveValue = board.PlyCount < 5 ? rng.Next(20) : 0; // RNG for fresh games
            Piece movingPiece = board.GetPiece(move.StartSquare);
            Piece capturedPiece = board.GetPiece(move.TargetSquare);
            int movingPieceValue = pieceEstimate((int)movingPiece.PieceType, movingPiece);
            int capturedPieceValue = pieceEstimate((int)capturedPiece.PieceType, capturedPiece);

            // Open with knights when reasonable
            if (depth == 0 && board.PlyCount <= 3 && movingPiece.IsKnight && (move.TargetSquare.File == 2 || move.TargetSquare.File == 5))
            {
                moveValue += 200;
            }

            // If you see checkmate, that's probably good
            if (MoveIsCheckmate(board, move))
            {
                moveValue += 5000;
            }

            // Draw value depends on if losing
            if (MoveIsDraw(board, move))
            {
                moveValue += losing ? 25 : -100;
            }

            // Add captured value
            moveValue += capturedPieceValue;

            // Encourage capture if not losing
            if (losing)
            {
                moveValue += (capturedPieceValue / 100);
            }

            // Try not to move high value pieces early
            if (board.PlyCount < 40)
            {
                moveValue -= (movingPieceValue / 100);
            }

            // Push pawns in end game
            if (movingPiece.IsPawn && board.PlyCount >= 40)
            {
                moveValue += 25;
            }

            // Center Ranks and Files
            if (board.PlyCount > 40 || (!movingPiece.IsKing && !movingPiece.IsQueen && !movingPiece.IsBishop))
            {
                if (move.TargetSquare.Rank == 3 || move.TargetSquare.Rank == 4 || move.TargetSquare.File == 2 || move.TargetSquare.File == 5)
                {
                    moveValue += 5;
                }
                if (move.TargetSquare.File == 3 || move.TargetSquare.File == 4)
                {
                    moveValue += 10;
                }
            }

            // On the edge
            if (movingPiece.Square.Rank == 0 || movingPiece.Square.Rank == 7 || movingPiece.Square.File == 0 || movingPiece.Square.File == 7)
            {
                if (movingPiece.IsKnight)
                {
                    moveValue -= 50;
                }
                if (movingPiece.IsBishop || movingPiece.IsKing && board.PlyCount <= 40)
                {
                    moveValue += 50;
                }
            }

            // Check, it might lead to mate, less so in end game
            if (MoveIsCheck(board, move))
            {
                moveValue += board.PlyCount <= 30 ? 60 : 30;
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
            bool dangerousSquare = board.SquareIsAttackedByOpponent(move.TargetSquare) || board.SquareIsAttackedByOpponent(move.StartSquare);
            if (dangerousSquare)
            {
                moveValue -= movingPieceValue;
            }

            if (MoveIsDefended(board, move))
            {
                moveValue += 200;
                if (depth == 0)
                {
                    System.Diagnostics.Debug.WriteLine(move.ToString());
                }
            }

            // If the move is promising and time permits, consider the future carefully
            depthValue = 0;
            if (depth <= maxDepth && (depth != maxDepth ||
                    (timer.MillisecondsRemaining > 10 * 1000 && moveValue >= highestValueMove)
                ))
            {
                // Undo lazy depth check
                if (dangerousSquare)
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
        if (depth == 0)
        {
            System.Diagnostics.Debug.WriteLine(board.PlyCount + "th turn: " + moveToPlay.MovePieceType.ToString() + " " + moveToPlay.ToString() + "-" + highestValueMove + " | Depth Value: " + depthValue + " | Eval: " + evaluation);
        }

        return Tuple.Create(moveToPlay, highestValueMove, evaluation);
    }

    // Compare moves for sorting
    int rankMoveForSorting(Board board, Move move)
    {
        int sortValue = 0;

        // Capturing high value pieces is often a good place to start
        Piece capturedPiece = board.GetPiece(move.TargetSquare);
        int capturedPieceValue = pieceEstimate((int)capturedPiece.PieceType, capturedPiece);
        sortValue = capturedPieceValue;

        // Other signs a move is good
        if (MoveIsCheck(board, move) || move.IsCastles)
        {
            sortValue += 100;
        }

        return sortValue;
    }

    // Estimate value of a piece
    int pieceEstimate(int pieceIndex, Piece piece = new Piece())
    {
        int pieceValue = pieceValues[pieceIndex];
        if (!piece.IsNull)
        {
            if (piece.IsPawn)
            {
                // Pawns close to promotion are more valuable
                if (((piece.IsWhite && piece.Square.Rank == 5) || (!piece.IsWhite && piece.Square.Rank == 2)))
                {
                    pieceValue += 100;
                }
                if (((piece.IsWhite && piece.Square.Rank == 6) || (!piece.IsWhite && piece.Square.Rank == 1)))
                {
                    pieceValue += 400;
                }
                if (piece.Square.File == 3 || piece.Square.File == 4)
                {
                    pieceValue += 50;
                }
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

    // Test if this move gives checkmate
    bool MoveIsDefended(Board board, Move move)
    {
        board.MakeMove(move);
        bool isDefended = board.SquareIsAttackedByOpponent(move.TargetSquare);
        board.UndoMove(move);
        return isDefended;
    }
}