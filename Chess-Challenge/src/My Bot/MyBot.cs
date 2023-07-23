using ChessChallenge.API;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Diagnostics;

public class MyBot : IChessBot
{
    int maxDepth = 6;
    int[] pieceValues = { 0, 100, 410, 420, 630, 1200, 5000 };

    public Move Think(Board board, Timer timer)
    {
        var moveChoice = BestMove(board, timer, 1, false, false);
        return moveChoice.Item1;
    }

    public Tuple<Move, int, int> BestMove(Board board, Timer timer, int depth, bool previousMoveWasCheck, bool previousMoveWasCapture)
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
        int evaluation = boardEval(board, pieces);

        // Sort moves for best candidates first
        Move[] sortedMoves = allMoves.OrderByDescending(thisMove => rankMoveForSorting(board, thisMove)).ToArray();

        foreach (Move move in sortedMoves)
        {
            // Get data
            int moveValue = 0;
            Piece movingPiece = board.GetPiece(move.StartSquare);
            Piece capturedPiece = board.GetPiece(move.TargetSquare);
            int movingPieceValue = pieceEval(board, movingPiece);
            int capturedPieceValue = pieceEval(board, capturedPiece);
            bool losing = evaluation < -50 && board.IsWhiteToMove || evaluation > 50 && !board.IsWhiteToMove;

            // Start with short term eval changes
            moveValue = (evaluationAfterMove(board, move, timer) - evaluation) * (board.IsWhiteToMove ? 1 : -1);

            // Open with knights with reasonable
            if (depth == 1 && board.PlyCount <= 3 && movingPiece.IsKnight && (move.TargetSquare.File == 2 || move.TargetSquare.File == 5))
            {
                moveValue += 100;
            }

            // If you see checkmate, that's probably good
            if (MoveIsCheckmate(board, move))
            {
                moveValue += 9000;
            }

            // Check, it might lead to mate, less so in end game
            bool isCheck = MoveIsCheck(board, move);
            if (isCheck)
            {
                moveValue += board.PlyCount <= 60 ? 30 : 10;
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
            if (MoveIsDraw(board, move))
            {
                moveValue += losing ? 100 : -50;
            }

            // Encourage capture if not losing
            if (!losing)
            {
                moveValue += (capturedPieceValue / 100);
            }

            // Prefer to move to defended squares
            if (MoveIsDefended(board, move))
            {
                moveValue += 5;
            }

            // Try not to move high value pieces
            moveValue -= (movingPieceValue / 100);

            // Push pawns in end game
            if (movingPiece.IsPawn && board.PlyCount >= 60)
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
            bool decentMove = moveValue > highestValueMove;
            if (
                depth <= maxDepth &&
                (depth <= maxDepth - 4 || board.PlyCount > 4) &&
                (depth <= maxDepth - 4 || timer.MillisecondsRemaining > 5000) &&
                (depth <= maxDepth - 4 || decentMove) &&
                (depth <= maxDepth - 4 || isCheck || previousMoveWasCheck || move.IsCapture || previousMoveWasCapture) &&
                (depth <= maxDepth - 3 || decentMove) &&
                (depth <= maxDepth - 3 || isCheck || previousMoveWasCheck) &&
                (depth <= maxDepth - 3 || move.IsCapture || previousMoveWasCapture) &&
                (depth <= maxDepth - 2 || decentMove) &&
                (depth <= maxDepth - 2 || isCheck || previousMoveWasCheck) &&
                (depth <= maxDepth - 2 || move.IsCapture || previousMoveWasCapture) &&
                (depth <= maxDepth - 1 || decentMove) &&
                (depth <= maxDepth - 1 || isCheck || previousMoveWasCheck) &&
                (depth <= maxDepth - 1 || move.IsCapture || previousMoveWasCapture)
                )
            {
                if (depth >= maxDepth)
                {
                    //Debug.WriteLine(depth);
                }
                // Undo lazy depth check, but keep some disincentive
                if (dangerousSquare)
                {
                    moveValue += movingPieceValue;
                    moveValue -= 10;
                }
                board.MakeMove(move);
                depthValue = BestMove(board, timer, depth + 1, isCheck, move.IsCapture).Item2 * -1;
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

        String log = board.PlyCount / 2 + " Turn: " + moveToPlay.MovePieceType.ToString() + " " + moveToPlay.ToString() + "-" + highestValueMove + " | Eval: " + evaluation + " | Depth Value: " + depthValue;
        Debug.WriteLineIf(depth == 1, log);

        return Tuple.Create(moveToPlay, highestValueMove, evaluation);
    }

    // Compare moves for sorting
    int rankMoveForSorting(Board board, Move move)
    {
        // Capturing high value pieces is often a good place to start
        int capturedPieceValue = pieceEval(board, board.GetPiece(move.TargetSquare));
        int sortValue = capturedPieceValue;

        // Other signs a move is good
        if (MoveIsCheck(board, move) || move.IsCastles)
        {
            sortValue += 100;
        }

        return sortValue;
    }

    // Get simple board eval
    int boardEval(Board board, PieceList[] listings)
    {
        int eval = 0;
        foreach (PieceList pieceList in listings)
        {
            // Bonus for having Bishop Pair
            if ((int)pieceList.TypeOfPieceInList == 3 && pieceList.Count == 2)
            {
                eval += 30;
            }
            if ((int)pieceList.TypeOfPieceInList == 4 && pieceList.Count == 2)
            {
                eval += 30;
            }
            foreach (Piece piece in pieceList)
            {
                eval+= pieceEval(board, piece) * (piece.IsWhite ? 1 : -1);
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
                pieceValue += 100;
            }
            if ((piece.IsWhite && piece.Square.Rank == 6) || (!piece.IsWhite && piece.Square.Rank == 1))
            {
                pieceValue += 400;
            }
            // Center pawns better than outside pawns
            if ((piece.IsWhite && piece.Square.File == 3) || (!piece.IsWhite && piece.Square.File == 4))
            {
                pieceValue += 20;
            }
            if ((piece.IsWhite && piece.Square.File == 0) || (!piece.IsWhite && piece.Square.File == 7))
            {
                pieceValue -= 20;
            }
            // Better if in front of king
            if (piece.Square.File == kingSquare.File)
            {
                pieceValue += 50;
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
        if (piece.IsKnight)
        {
            // Knight on the rim is dim
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
                pieceValue += 50;
            }
        }
        if (Math.Abs(kingSquare.File - piece.Square.File) <= 2 || Math.Abs(kingSquare.Rank - piece.Square.Rank) <= 2)
        {
            pieceValue += 30;
        }
        if (Math.Abs(kingSquare.File - piece.Square.File) <= 2 && Math.Abs(kingSquare.Rank - piece.Square.Rank) <= 2)
        {
            pieceValue += 30;
        }
        // Rooks and pawns better in end game
        if (piece.IsPawn || piece.IsRook)
        {
            pieceValue += board.PlyCount / 4;
        }
        return pieceValue;
    }

    int evaluationAfterMove(Board board, Move move, Timer timer)
    {
        board.MakeMove(move);
        PieceList[] pieces = board.GetAllPieceLists();
        int evaluation = boardEval(board, pieces);
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