using ChessChallenge.API;
using System;

public class MyBot : IChessBot
{
    // Piece values: null, pawn, knight, bishop, rook, queen, king
    int[] pieceValues = { 0, 100, 250, 300, 500, 900, 2000 };

    public Move Think(Board board, Timer timer)
    {
        //System.Threading.Thread.Sleep(500);
        Move[] allMoves = board.GetLegalMoves();

        // Pick a random move to play if nothing better is found
        Random rng = new();
        Move moveToPlay = allMoves[rng.Next(allMoves.Length)];
        int highestValueMove = 0;

        foreach (Move move in allMoves)
        {
            // Always play checkmate in one
            if (MoveIsCheckmate(board, move))
            {
                System.Diagnostics.Debug.WriteLine("Checkmate");
                moveToPlay = move;
                break;
            }

            // Always castle
            if (move.IsCastles)
            {
                moveToPlay = move;
                break;
            }

            // Find info about pieces
            Piece movingPiece = board.GetPiece(move.StartSquare);
            Piece capturedPiece = board.GetPiece(move.TargetSquare);
            int movingPieceValue = pieceValues[(int)movingPiece.PieceType];
            int capturedPieceValue = pieceValues[(int)capturedPiece.PieceType];

            // Open with knights
            if (movingPiece.IsKnight && board.PlyCount <= 2 && (move.TargetSquare.File == 3 || move.TargetSquare.File == 6))
            {
                moveToPlay = move;
                break;
            }

            // Start with captured value
            int moveValue = capturedPieceValue;

            // When no captured piece
            if (capturedPieceValue <= 0)
            {
                // Try not to move high value pieces
                moveValue -= (movingPieceValue / 100);

                // Check, it might lead to mate
                if (MoveIsCheck(board, move))
                {
                    moveValue += 100;
                }

                // Bias towards center Ranks and Files
                if (move.TargetSquare.Rank == 4 || move.TargetSquare.Rank == 5)
                {
                    moveValue += 50;
                }
                if (move.TargetSquare.File == 3 || move.TargetSquare.File == 6)
                {
                    moveValue += 50;
                }
                if (move.TargetSquare.File == 4 || move.TargetSquare.File == 5)
                {
                    moveValue += 100;
                }

                // Push pawns in end game
                if (movingPiece.IsPawn && board.PlyCount >= 30)
                {
                    moveValue += 100;
                }
            }

            // Avoid draws
            if (MoveIsDraw(board, move))
            {
                moveValue -= 10000;
            }

            // Avoid attacked squares
            if (board.SquareIsAttackedByOpponent(move.TargetSquare))
            {
                moveValue -= movingPieceValue;
            }

            // Move away from attacked squares
            if (board.SquareIsAttackedByOpponent(move.StartSquare))
            {
                moveValue += movingPieceValue;
            }

            // Always promote to queen
            if ((int)move.PromotionPieceType == 5)
            {
                moveToPlay = move;
                break;
            }

            // Use move if highest so far
            if (moveValue > highestValueMove)
            {
                highestValueMove = moveValue;
                moveToPlay = move;
            }
        }
        System.Diagnostics.Debug.WriteLine(highestValueMove);

        return moveToPlay;
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