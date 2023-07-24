using ChessChallenge.API;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Diagnostics;
using Microsoft.CodeAnalysis;

public class MyBot : IChessBot
{
    int maxDepth = 6;
    int thinks = 0;
    int[] pieceValues = { 0, 100, 350, 350, 600, 1200, 5000 };

    public Move Think(Board board, Timer timer)
    {
        thinks = 0;
        Move choice = BestMove(board, timer, 1, false, false, false).Item1;
        return choice;
    }

    public Tuple<Move, int, int> BestMove(Board board, Timer timer, int depth, bool previousMoveWasCheck, bool previousMoveWasCapture, bool previousMoveWasPieceCapture)
    {
        // Get data and prep for loop
        Move[] allMoves = board.GetLegalMoves();
        if (allMoves.Length == 0)
        {
            return Tuple.Create(new Move(), 0, 0);
        }
        Move moveToPlay = allMoves[0];
        Move secondChoice = allMoves[0];
        int highestValueMove = -10000;
        int depthValue = 2;
        PieceList[] pieces = board.GetAllPieceLists();
        int evaluation = boardEval(board, pieces);
        thinks++;

        foreach (Move move in allMoves)
        {
            // Get data
            int moveValue = 0;
            if (board.PlyCount < 10)
            {
                Random rng = new();
                moveValue = rng.Next(5);
            }
            Piece movingPiece = board.GetPiece(move.StartSquare);
            Piece capturedPiece = board.GetPiece(move.TargetSquare);
            int movingPieceValue = pieceEval(board, movingPiece);
            int capturedPieceValue = pieceEval(board, capturedPiece);
            bool losing = evaluation < -200 && board.IsWhiteToMove || evaluation > 200 && !board.IsWhiteToMove;
            bool winning = evaluation > 200 && board.IsWhiteToMove || evaluation < -200 && !board.IsWhiteToMove;

            Tuple<int, bool, bool, bool, bool> futureInfo = lookAhead(board, move);
            int nextEvaulation = futureInfo.Item1;
            bool isDraw = futureInfo.Item2;
            bool isCheck = futureInfo.Item3;
            bool isMate = futureInfo.Item4;
            bool isDefended = futureInfo.Item5;

            // Start with short term eval changes
            moveValue += (nextEvaulation - evaluation) * (board.IsWhiteToMove ? 1 : -1);

            // Open with knights if reasonable
            if (board.PlyCount <= 4 && movingPiece.IsKnight)
            {
                moveValue += 10;
            }

            // If you see checkmate, that's probably good
            if (isMate)
            {
                moveValue += 99000;
            }

            // Check, it might lead to mate, less so in end game
            if (isCheck)
            {
                moveValue += board.PlyCount <= 60 ? 20 : 10;
            }

            // Castling
            if (move.IsCastles)
            {
                moveValue += 50;
            }
            // Preserve castling
            else if (board.PlyCount <= 10 && movingPiece.IsKing || movingPiece.IsRook)
            {
                moveValue -= 30;
            }

            // Promote to queen
            if ((int)move.PromotionPieceType == 5)
            {
                moveValue += 1000;
            }

            // Avoid moving flank pawns
            if (movingPiece.IsPawn && (move.StartSquare.File <= 1 || move.StartSquare.File >= 6))
            {
                moveValue -= 20;
            }

            // Draw value depends on winning vs losing
            if (isDraw)
            {
                moveValue += losing ? 100 : -100;
            }

            // Encourage capture if winning
            if (!winning)
            {
                moveValue += (capturedPieceValue / 100);
            }

            // Prefer to move to defended squares
            if (isDefended)
            {
                moveValue += 5;
            }

            // Try not to move high value pieces
            moveValue -= (movingPieceValue / 100);

            // Push pawns in end game
            if (movingPiece.IsPawn && board.PlyCount >= 60)
            {
                moveValue += 20;
            }

            // Lazy depth check by avoiding staying on or targeting attacked squares at end of depth
            bool dangerousSquare = board.SquareIsAttackedByOpponent(move.TargetSquare) || board.SquareIsAttackedByOpponent(move.StartSquare);
            if (dangerousSquare)
            {
                moveValue -= movingPieceValue;
            }

            // If the move is promising and time permits, consider the future carefully
            depthValue = 0;
            bool recentChecks = isCheck || previousMoveWasCheck;
            bool pieceCapture = move.IsCapture && !capturedPiece.IsPawn;
            if (
                depth <= maxDepth &&
                (depth < 2 || moveValue + 200 > highestValueMove || recentChecks || move.IsCapture || previousMoveWasCapture) &&
                (depth < 3 || board.PlyCount > 6) &&
                (depth < 3 || timer.MillisecondsRemaining > 5000) &&
                (depth < 3 || moveValue + 200 > highestValueMove) &&
                (depth < 3 || recentChecks || pieceCapture || (move.IsCapture && previousMoveWasCapture)) &&
                (depth < 4 || recentChecks || pieceCapture || previousMoveWasPieceCapture) &&
                (depth < 5 || (recentChecks && (pieceCapture && previousMoveWasPieceCapture)))
                )
            {
                // Undo lazy depth check, but keep some disincentive
                if (dangerousSquare)
                {
                    moveValue += movingPieceValue;
                    moveValue -= (movingPieceValue / 20);
                }

                // See what the future holds
                board.MakeMove(move);
                depthValue = BestMove(board, timer, depth + 1, isCheck, move.IsCapture, pieceCapture).Item2 * -1;
                moveValue += depthValue;
                board.UndoMove(move);
            }

            // Use move if highest so far
            if (moveValue > highestValueMove)
            {
                highestValueMove = moveValue;
                secondChoice = moveToPlay;
                moveToPlay = move;
            }
        }

        String log = "Turn: " + (board.PlyCount / 2) +
            " | Thinks: " + thinks +
            " | Time: " + (timer.MillisecondsRemaining / 1000) +
            " | Eval: " + evaluation + 
            " | " + moveToPlay.MovePieceType.ToString() + " " + moveToPlay.ToString() + 
            " | Value: " + highestValueMove +
            " | Depth Value: " + depthValue +
            " | Second Move " + secondChoice.MovePieceType.ToString() + " " + secondChoice.ToString();
        Debug.WriteLineIf(depth == 1, log);

        return Tuple.Create(moveToPlay, highestValueMove, evaluation);
    }

    // Get simple board eval
    int boardEval(Board board, PieceList[] listings)
    {
        int eval = 0;
        Piece lastPawn = new Piece();
        foreach (PieceList pieceList in listings)
        {
            // Bonus for having Bishop Pair
            if ((int)pieceList.TypeOfPieceInList == 3 && pieceList.Count == 2)
            {
                eval += 50;
            }
            // Better with two rooks
            if ((int)pieceList.TypeOfPieceInList == 4 && pieceList.Count == 2)
            {
                eval += 30;
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
        Square kingSquare = board.GetKingSquare(!piece.IsWhite);
        if (piece.IsPawn)
        {
            // Pawns better closer to promotion
            if ((piece.IsWhite && piece.Square.Rank == 5) || (!piece.IsWhite && piece.Square.Rank == 2))
            {
                pieceValue += 50;
            }
            if ((piece.IsWhite && piece.Square.Rank == 6) || (!piece.IsWhite && piece.Square.Rank == 1))
            {
                pieceValue += 300;
            }
            // Center pawns good
            if ((piece.IsWhite && piece.Square.File == 3) || (!piece.IsWhite && piece.Square.File == 4))
            {
                pieceValue += 20;
            }
            // Flank pawns bad
            if ((piece.IsWhite && piece.Square.File <= 1) || (!piece.IsWhite && piece.Square.File >= 6))
            {
                pieceValue -= 10;
            }
            // Better if in front of king
            if (Math.Abs(piece.Square.File - kingSquare.File) <= 1 && Math.Abs(piece.Square.Rank - kingSquare.Rank) == 1)
            {
                pieceValue += 20;
            }
        }
        // Enjoy the center
        if (!piece.IsKing)
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
        // Knights on the rim are dim
        if (piece.IsKnight)
        {
            if (piece.Square.Rank == 0 || piece.Square.Rank == 7 || piece.Square.File == 0 || piece.Square.File == 7)
            {
                pieceValue -= 30;
            }

        }
        // Piece is lined up with opponnent King
        if (piece.IsRook || piece.IsQueen)
        {
            if (piece.Square.File == kingSquare.File || piece.Square.Rank == kingSquare.Rank)
            {
                pieceValue += 40;
            }
            if (Math.Abs(kingSquare.File - piece.Square.File) <= 1 || Math.Abs(kingSquare.Rank - piece.Square.Rank) <= 1)
            {
                pieceValue += 20;
            }
        }
        // Piece is diagnal with opponnent King
        if (piece.IsBishop || piece.IsQueen)
        {
            if (Math.Abs(kingSquare.File - piece.Square.File) == Math.Abs(kingSquare.Rank - piece.Square.Rank))
            {
                pieceValue += 20;
            }
        }
        // In opponnent King area
        if (Math.Abs(kingSquare.File - piece.Square.File) <= 3 && Math.Abs(kingSquare.Rank - piece.Square.Rank) <= 3)
        {
            pieceValue += 10;
        }
        return pieceValue;
    }

    // Get evaluation after move is made
    public Tuple<int, bool, bool, bool, bool> lookAhead(Board board, Move move)
    {
        board.MakeMove(move);
        PieceList[] pieces = board.GetAllPieceLists();
        int evaluation = boardEval(board, pieces);
        bool isDraw = board.IsDraw();
        bool isCheck = board.IsInCheck();
        bool isMate = board.IsInCheckmate();
        bool isDefended = board.SquareIsAttackedByOpponent(move.TargetSquare);
        board.UndoMove(move);
        return Tuple.Create(evaluation, isDraw, isCheck, isMate, isDefended);
    }
}