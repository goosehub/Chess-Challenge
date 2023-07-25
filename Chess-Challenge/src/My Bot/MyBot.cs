using ChessChallenge.API;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Raylib_cs;

public class MyBot : IChessBot
{
    int maxDepth = 6;
    int thinks = 0;
    int[] pieceValues = { 0, 100, 350, 350, 600, 1200, 5000 };

    public Move Think(Board board, Timer timer)
    {
        thinks = 0;
        //return BestMove(board, timer, 1, false, false, false).Item1;
        Tuple<Move, int> choice = BestMove(board, timer, 1, false, false, false);
        string log = "Turn:"+(board.PlyCount / 2)+" | "+choice.Item1.MovePieceType.ToString()+" "+choice.Item1.ToString()+" | Eval:"+((double)choice.Item2/100)+" | Time:"+(timer.MillisecondsRemaining / 1000)+" | Thinks:"+thinks;
        Debug.WriteLine(log);
        return choice.Item1;
    }

    // Compare moves for sorting
    int rankMoveForSorting(Board board, Move move)
    {
        // Capturing high value pieces is often a good place to start
        int sortValue = pieceEval(board, board.GetPiece(move.TargetSquare));

        // Saving a piece is often good
        if (board.SquareIsAttackedByOpponent(move.StartSquare))
        {
            sortValue += pieceEval(board, board.GetPiece(move.StartSquare));
        }

        // Castling is often the best move
        if (move.IsCastles)
        {
            sortValue += 500;
        }

        return sortValue;
    }

    public Tuple<Move, int> BestMove(Board board, Timer timer, int depth, bool previousMoveWasCheck, bool previousMoveWasCapture, bool previousMoveWasPieceCapture)
    {
        // Prepare to do the loop de loop
        if (depth == maxDepth - 2)
        {
            thinks++;
        }
        Move[] allMoves = board.GetLegalMoves();

        // Sort moves for best candidates first
        Move[] sortedMoves = allMoves.OrderByDescending(thisMove => rankMoveForSorting(board, thisMove)).ToArray();

        Move moveToPlay = allMoves[0];
        bool whiteToMove = board.IsWhiteToMove;
        int bestEvaluation = whiteToMove ? -99999 : 99999;

        foreach (Move move in sortedMoves)
        {
            board.MakeMove(move);
            int moveEvaluation = boardEval(board);
            Piece movingPiece = board.GetPiece(move.StartSquare);
            Piece capturedPiece = board.GetPiece(move.TargetSquare);
            bool recentChecks = board.IsInCheck() || previousMoveWasCheck;
            bool pieceCapture = move.IsCapture && !capturedPiece.IsPawn;

            // Consider the future carefully
            if (
                depth <= maxDepth && !board.IsDraw() && !board.IsInCheckmate() &&
                (depth < 3 || timer.MillisecondsRemaining > 10000) &&
                (depth < 3 || (whiteToMove ? moveEvaluation + 200 > bestEvaluation : moveEvaluation - 200 < bestEvaluation)) &&
                (depth < 3 || recentChecks || pieceCapture || (move.IsCapture && previousMoveWasPieceCapture)) &&
                (depth < 4 || (whiteToMove ? moveEvaluation > bestEvaluation : moveEvaluation < bestEvaluation)) &&
                (depth < 5 || (recentChecks && pieceCapture && previousMoveWasPieceCapture))
                )
            {
                moveEvaluation = BestMove(board, timer, depth + 1, board.IsInCheck(), move.IsCapture, pieceCapture).Item2;
            }

            // Castling is outside evaluation
            if (move.IsCastles)
            {
                moveEvaluation += whiteToMove ? 100 : -100;
            }
            else if (board.PlyCount <= 16 && movingPiece.IsKing || movingPiece.IsRook)
            {
                moveEvaluation -= whiteToMove ? 10 : -10;
            }

            // Use the best outcome
            if ((whiteToMove && moveEvaluation > bestEvaluation) || (!whiteToMove && moveEvaluation < bestEvaluation))
            {
                bestEvaluation = moveEvaluation;
                moveToPlay = move;
            }
            board.UndoMove(move);
        }

        return Tuple.Create(moveToPlay, bestEvaluation);
    }

    // Get simple board eval
    int boardEval(Board board)
    {
        int eval = 0;
        PieceList[] pieces = board.GetAllPieceLists();

        // Checkmate suckers
        if (board.IsInCheckmate())
        {
            return board.IsWhiteToMove ? -88888 : 88888;
        }

        // Draw
        if (board.IsDraw())
        {
            return 0;
        }

        Piece lastPawn = new Piece();
        foreach (PieceList pieceList in pieces)
        {
            // Bishop Pair
            if ((int)pieceList.TypeOfPieceInList == 3 && pieceList.Count == 2)
            {
                eval += 50;
            }
            foreach (Piece piece in pieceList)
            {
                if ((int)pieceList.TypeOfPieceInList == 1)
                {
                    if (!lastPawn.IsNull && lastPawn.IsWhite == piece.IsWhite)
                    {
                        // Doubled pawns bad
                        if (lastPawn.Square.File == piece.Square.File)
                        {
                            eval -= 10;
                        }
                        // Connected pawns good
                        if (Math.Abs(lastPawn.Square.File - piece.Square.File) == 1 && Math.Abs(lastPawn.Square.Rank - piece.Square.Rank) == 1)
                        {
                            eval += 10;
                        }
                    }
                    lastPawn = piece;
                }
                eval += pieceEval(board, piece) * (piece.IsWhite ? 1 : -1);
            }
        }
        return eval;
    }

    // Estimate value of a piece
    int pieceEval(Board board, Piece piece = new Piece())
    {
        int pieceValue = pieceValues[(int)piece.PieceType];
        Square ownKingSquare = board.GetKingSquare(piece.IsWhite);
        Square otherKingSquare = board.GetKingSquare(!piece.IsWhite);
        // Pawns
        if (piece.IsPawn)
        {
            // Move in early game
            if (board.PlyCount < 10 && (piece.Square.File == 3 || piece.Square.File == 4) && (piece.Square.Rank < 2 || piece.Square.Rank > 5))
            {
                pieceValue -= 60;
            }
            // Pawns better closer to promotion
            if (piece.Square.Rank == (piece.IsWhite ? 4 : 3))
            {
                pieceValue += 30;
            }
            if (piece.Square.Rank == (piece.IsWhite ? 5 : 2))
            {
                pieceValue += 100;
            }
            if (piece.Square.Rank == (piece.IsWhite ? 6 : 1))
            {
                pieceValue += 500;
            }
            // Center pawns good
            if ((piece.IsWhite && piece.Square.File == 3) || (!piece.IsWhite && piece.Square.File == 4))
            {
                pieceValue += 30;
            }
            // Flank pawns less good
            if ((piece.IsWhite && piece.Square.File <= 1) || (!piece.IsWhite && piece.Square.File >= 6))
            {
                pieceValue -= 10;
                // Early pushed to center flank pawns are even less good
                if (board.PlyCount < 40 && (piece.Square.Rank == 3 || piece.Square.Rank == 4))
                {
                    pieceValue -= 30;
                }
            }
            // Pawns better if in front of their King
            if (Math.Abs(piece.Square.File - ownKingSquare.File) <= 1 && Math.Abs(piece.Square.Rank - ownKingSquare.Rank) <= 2)
            {
                pieceValue += 30;
            }
        }
        // Get your pieces out
        if (board.PlyCount < 16 && (piece.IsKnight || piece.IsBishop) && piece.Square.Rank == (piece.IsWhite ? 0 : 7))
        {
            pieceValue -= 80;
        }
        // King
        if (piece.IsKing)
        {
            if (board.PlyCount < 60)
            {
                // King has lost right to castle
                if (piece.Square.File == 3 || piece.Square.File == 5)
                {
                    pieceValue -= 50;
                }
            }
            else
            {
                // Approach other King
                if (Math.Abs(piece.Square.File - otherKingSquare.File) <= 2 && Math.Abs(piece.Square.Rank - otherKingSquare.Rank) <= 2)
                {
                    pieceValue += 30;
                }
            }
        }
        // Knights
        if (piece.IsKnight)
        {
            // Knights on the rim are dim
            if (piece.Square.Rank == 0 || piece.Square.Rank == 7 || piece.Square.File == 0 || piece.Square.File == 7)
            {
                pieceValue -= 30;
            }
        }
        // Queen should stay out of danger
        if (piece.IsQueen && piece.IsWhite == board.IsWhiteToMove && board.SquareIsAttackedByOpponent(piece.Square))
        {
            pieceValue -= 50;
        }
        // Enjoy the center
        if (!piece.IsKing || board.PlyCount > 80)
        {
            if (piece.Square.Rank == 3 || piece.Square.Rank == 4 || piece.Square.File == 2 || piece.Square.File == 5)
            {
                pieceValue += 10;
            }
            if (piece.Square.File == 3 || piece.Square.File == 4)
            {
                pieceValue += 20;
            }
        }
        // Piece horizontal or vertical with opponnent King
        if (piece.IsRook || piece.IsQueen)
        {
            if (piece.Square.File == otherKingSquare.File || piece.Square.Rank == otherKingSquare.Rank)
            {
                pieceValue += 20;
            }
            if (Math.Abs(otherKingSquare.File - piece.Square.File) <= 1 || Math.Abs(otherKingSquare.Rank - piece.Square.Rank) <= 1)
            {
                pieceValue += 10;
            }
        }
        // Piece diagnal with opponnent King
        if (piece.IsBishop || piece.IsQueen)
        {
            if (Math.Abs(otherKingSquare.File - piece.Square.File) == Math.Abs(otherKingSquare.Rank - piece.Square.Rank))
            {
                pieceValue += 20;
            }
        }
        // In opponnent King area
        if (Math.Abs(otherKingSquare.File - piece.Square.File) <= 3 && Math.Abs(otherKingSquare.Rank - piece.Square.Rank) <= 3)
        {
            pieceValue += 10;
        }
        return pieceValue;
    }
}