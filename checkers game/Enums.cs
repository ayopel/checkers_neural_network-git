using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace checkersclaude
{
    public enum PieceColor
    {
        Red,
        Black
    }

    public enum PieceType
    {
        Regular,
        King
    }

    public enum GameState
    {
        RedTurn,
        BlackTurn,
        RedWins,
        BlackWins,
        draw
    }
    public enum GameMode
    {
        HumanVsHuman,
        HumanVsAI
    }
}