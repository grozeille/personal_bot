using System.Drawing;
using TabletApp.UI;

namespace TabletApp.Tests.Support;

/// <summary>
/// Sortie simulée : <c>ConfirmDialog</c> en mode sortie (écran 800×600).
/// </summary>
internal sealed class SimulatedQuit
{
    public const float ScreenWidth = 800;
    public const float ScreenHeight = 600;

    public ConfirmDialog Confirm { get; } = new();

    public bool QuitRequested { get; private set; }

    public RobotExpression Current { get; private set; } = RobotExpression.Neutral;

    /// <summary>Tap en pixels écran (comme MainForm : coin puis yeux).</summary>
    public void Tap(float x, float y)
    {
        if (Confirm.Active)
        {
            (float vx, float vy) = FacePainter.ToView(x, y, (int)ScreenWidth, (int)ScreenHeight);
            if (Confirm.Tap(vx, vy) == ConfirmTap.Confirmed
                && Confirm.Purpose == ConfirmPurpose.Quit)
            {
                QuitRequested = true;
            }
            else if (!Confirm.Active)
            {
                Current = Confirm.Previous;
            }
        }
        else if (ConfirmDialog.QuitHit(new PointF(x, y)))
        {
            Confirm.Show(Current, ConfirmPurpose.Quit);
            Current = RobotExpression.Confirm;
        }
    }
}
