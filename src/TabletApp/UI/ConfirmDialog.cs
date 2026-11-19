using System.Drawing;

namespace TabletApp.UI;

/// <summary>Résultat d'un tap pendant un dialogue de confirmation.</summary>
internal enum ConfirmTap
{
    Ignored,
    Cancelled,
    Confirmed,
}

/// <summary>Ce que le tap sur l'œil ✓ déclenche.</summary>
internal enum ConfirmPurpose
{
    Update,
    Quit,
}

/// <summary>
/// Dialogue de confirmation (anti-enfant sans appui long, Windows promouvant
/// l'appui tactile en clic droit) : un tap affiche le visage
/// <see cref="RobotExpression.Confirm"/>, puis un tap sur l'œil gauche (X)
/// annule et un tap sur l'œil droit (✓) confirme. Sert à la mise à jour
/// (depuis l'icône) comme à la sortie (depuis le coin haut-gauche).
/// Logique pure testable (tests BDD), coordonnées en repère vue 800×550.
/// </summary>
internal sealed class ConfirmDialog
{
    // Bande des yeux : couvre le X (y 251-324) et le ✓ (y 255-319) avec de
    // la marge tactile, bouche (y 368) exclue. Moitié gauche = annuler.
    private const float EyeTop = 170f;
    private const float EyeBottom = 340f;
    private const float MidX = 400f;
    private const float CornerZone = 120f;

    public bool Active { get; private set; }

    /// <summary>Expression à restaurer après annulation/confirmation.</summary>
    public RobotExpression Previous { get; private set; } = RobotExpression.Neutral;

    public ConfirmPurpose Purpose { get; private set; } = ConfirmPurpose.Update;

    public void Show(RobotExpression previous, ConfirmPurpose purpose)
    {
        Active = true;
        Previous = previous;
        Purpose = purpose;
    }

    /// <summary>Sortie externe (maj disparue) : l'appelant restaure le visage.</summary>
    public void Cancel() => Active = false;

    public ConfirmTap Tap(float viewX, float viewY)
    {
        if (!Active)
        {
            return ConfirmTap.Ignored;
        }
        if (viewY < EyeTop || viewY > EyeBottom)
        {
            return ConfirmTap.Ignored;
        }
        Active = false;
        return viewX < MidX ? ConfirmTap.Cancelled : ConfirmTap.Confirmed;
    }

    /// <summary>Zone de l'icône de maj (carré en haut à droite, pixels écran).</summary>
    public static bool IconHit(PointF location, float width) =>
        location.X >= width - CornerZone && location.X <= width
        && location.Y >= 0 && location.Y < CornerZone;

    /// <summary>Zone de sortie (carré en haut à gauche, pixels écran).</summary>
    public static bool QuitHit(PointF location) =>
        location.X >= 0 && location.Y >= 0
        && location.X < CornerZone && location.Y < CornerZone;
}
