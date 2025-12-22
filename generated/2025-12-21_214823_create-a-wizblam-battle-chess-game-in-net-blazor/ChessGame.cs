using System;
using System.Collections.Generic;

public class ChessPiece
{
    public string Type { get; set; }
    public bool IsWhite { get; set; }

    public virtual bool IsValidMove(int startX, int startY, int endX, int endY)
    {
        // Base implementation for valid move check
        return true;
    }
}

public class ChessBoard
{
    private ChessPiece[,] board = new ChessPiece[8, 8];

    public ChessPiece this[int x, int y]
    {
        get => board[x, y];
        set => board[x, y] = value;
    }

    public void Initialize()
    {
        // Set up the initial positions of the pieces
        for (int i = 0; i < 8; i++)
        {
            this[1, i] = new ChessPiece { Type = "Pawn", IsWhite = true };
            this[6, i] = new ChessPiece { Type = "Pawn", IsWhite = false };
        }
    }

    public bool MovePiece(int startX, int startY, int endX, int endY)
    {
        var piece = this[startX, startY];
        if (piece == null || !piece.IsValidMove(startX, startY, endX, endY))
            return false;

        this[endX, endY] = piece;
        this[startX, startY] = null;
        return true;
    }
}

public class ChessGame
{
    public ChessBoard Board { get; private set; }

    public ChessGame()
    {
        Board = new ChessBoard();
        Board.Initialize();
    }
}