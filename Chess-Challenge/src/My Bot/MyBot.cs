using ChessChallenge.API;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Diagnostics;

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
        int highestValueMove = -10000;
        int depthValue = 2;
        PieceList[] pieces = board.GetAllPieceLists();
        int evaluation = boardEval(pieces);

        // Sort moves for best candidates first
        Move[] sortedMoves = allMoves.OrderByDescending(thisMove => rankMoveForSorting(board, thisMove)).ToArray();

        foreach (Move move in sortedMoves)
        {
            // Get data
            int moveValue = 0;
            Piece movingPiece = board.GetPiece(move.StartSquare);
            Piece capturedPiece = board.GetPiece(move.TargetSquare);
            int movingPieceValue = pieceEval(movingPiece);
            int capturedPieceValue = pieceEval(capturedPiece);
            bool losing = evaluation < -50 && board.IsWhiteToMove || evaluation > 50 && !board.IsWhiteToMove;

            // Start with short term eval changes
            moveValue = (evaluationAfterMove(board, move, timer) - evaluation) * (board.IsWhiteToMove ? 1 : -1);

            // Open with knights with reasonable
            if (depth == 0 && board.PlyCount <= 3 && movingPiece.IsKnight && (move.TargetSquare.File == 2 || move.TargetSquare.File == 5))
            {
                moveValue += 100;
            }

            // If you see checkmate, that's probably good
            if (MoveIsCheckmate(board, move))
            {
                moveValue += 3000;
            }

            // Check, it might lead to mate, less so in end game
            if (MoveIsCheck(board, move))
            {
                moveValue += board.PlyCount <= 30 ? 60 : 30;
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

            // Draw value depends on winning vs losing
            if (MoveIsDraw(board, move))
            {
                moveValue += losing ? 25 : -200;
            }

            // Encourage capture if not losing
            if (!losing)
            {
                moveValue += (capturedPieceValue / 100);
            }

            // Prefer to move to defended squares
            if (MoveIsDefended(board, move))
            {
                moveValue += 25;
            }

            // Try not to move high value pieces
            moveValue -= (movingPieceValue / 100);

            // Push pawns in end game
            if (movingPiece.IsPawn && board.PlyCount >= 40)
            {
                moveValue += 25;
            }

            // Lazy depth check by avoiding staying on or targeting attacked squares at end of depth
            bool dangerousSquare = board.SquareIsAttackedByOpponent(move.TargetSquare) || board.SquareIsAttackedByOpponent(move.StartSquare);
            if (dangerousSquare)
            {
                moveValue -= movingPieceValue;
            }

            // If the move is promising and time permits, consider the future carefully
            depthValue = 0;
            if (depth <= maxDepth && (depth != maxDepth ||
                    (timer.MillisecondsRemaining > 10000 && moveValue >= highestValueMove)
                ))
            {
                // Undo lazy depth check
                if (dangerousSquare)
                {
                    moveValue += movingPieceValue;
                    moveValue -= 50;
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

        //String log = board.PlyCount / 2 + " Turn: " + moveToPlay.MovePieceType.ToString() + " " + moveToPlay.ToString() + "-" + highestValueMove + " | Eval: " + evaluation + " | Depth Value: " + depthValue;
        //Debug.WriteLineIf(depth == 0, log);

        return Tuple.Create(moveToPlay, highestValueMove, evaluation);
    }

    // Compare moves for sorting
    int rankMoveForSorting(Board board, Move move)
    {
        // Capturing high value pieces is often a good place to start
        int capturedPieceValue = pieceEval(board.GetPiece(move.TargetSquare));
        int sortValue = capturedPieceValue;

        // Other signs a move is good
        if (MoveIsCheck(board, move) || move.IsCastles)
        {
            sortValue += 100;
        }

        return sortValue;
    }

    // Get simple board eval
    int boardEval(PieceList[] listings)
    {
        int eval = 0;
        foreach (PieceList pieceList in listings)
        {
            // Bonus for having Bishop Pair
            if ((int)pieceList.TypeOfPieceInList == 3 && pieceList.Count == 2)
            {
                eval += 30;
            }
            foreach (Piece piece in pieceList)
            {
                eval+= pieceEval(piece) * (piece.IsWhite ? 1 : -1);
            }
        }
        return eval;
    }

    // Estimate value of a piece
    int pieceEval(Piece piece = new Piece())
    {
        int pieceValue = pieceValues[(int)piece.PieceType];
        // Pawns better closer to promotion
        if (piece.IsPawn)
        {
            if ((piece.IsWhite && piece.Square.Rank == 5) || (!piece.IsWhite && piece.Square.Rank == 2))
            {
                pieceValue += 100;
            }
            if ((piece.IsWhite && piece.Square.Rank == 6) || (!piece.IsWhite && piece.Square.Rank == 1))
            {
                pieceValue += 400;
            }
        }
        // Enjoy the center
        if (!piece.IsBishop || !piece.IsKing)
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
        if (piece.IsKnight)
        {
            // Knight on the rim is dim
            if (piece.Square.Rank == 0 || piece.Square.Rank == 7 || piece.Square.File == 0 || piece.Square.File == 7)
            {
                pieceValue -= 30;
            }

        }
        return pieceValue;
    }

    int evaluationAfterMove(Board board, Move move, Timer timer, int depth = 0)
    {
        board.MakeMove(move);
        PieceList[] pieces = board.GetAllPieceLists();
        int evaluation = boardEval(pieces);
        board.UndoMove(move);
        return evaluation;
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