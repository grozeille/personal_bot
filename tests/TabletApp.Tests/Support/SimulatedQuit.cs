using System.Drawing;
using TabletApp.UI;

namespace TabletApp.Tests.Support;

/// <summary>
/// Geste de sortie simulé : horloge + <c>QuitGesture</c> pour assertions.
/// </summary>
internal sealed class SimulatedQuit
{
    public QuitGesture Gesture { get; } = new();
    public TimeSpan Now { get; private set; }

    public void Down(float x, float y) =>
        Gesture.Down(new PointF(x, y), Now);

    public void Hold(TimeSpan duration)
    {
        TimeSpan end = Now + duration;
        while (Now < end)
        {
            Gesture.Tick(Now);
            Now += TimeSpan.FromMilliseconds(100);
        }
        Gesture.Tick(Now);
    }

    public void Release() => Gesture.Up(Now);
}
