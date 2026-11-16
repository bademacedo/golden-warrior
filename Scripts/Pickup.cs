using Godot;

/// <summary>Burger posé au sol : rend des points de vie.</summary>
public partial class Pickup : Node2D
{
    public int Heal = 30;
    private float _t;

    public override void _Process(double delta)
    {
        _t += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawSetTransformMatrix(new Transform2D(0, new Vector2(1, 0.3f), 0, Vector2.Zero));
        DrawCircle(Vector2.Zero, 16, new Color(0, 0, 0, 0.3f));

        float bob = Mathf.Sin(_t * 4) * 2 - 12;
        DrawSetTransformMatrix(new Transform2D(0, new Vector2(1, 0.6f), 0, new Vector2(0, bob - 6)));
        DrawCircle(Vector2.Zero, 15, new Color(0.85f, 0.55f, 0.2f)); // pain du haut
        DrawSetTransformMatrix(Transform2D.Identity);

        DrawRect(new Rect2(-15, bob - 4, 30, 3), new Color(0.3f, 0.75f, 0.2f));   // salade
        DrawRect(new Rect2(-14, bob - 1, 28, 5), new Color(0.4f, 0.2f, 0.1f));    // steak
        DrawRect(new Rect2(-14, bob + 4, 28, 5), new Color(0.85f, 0.55f, 0.2f));  // pain du bas
        DrawCircle(new Vector2(-5, bob - 12), 1.2f, new Color(1f, 0.95f, 0.8f));  // sésame
        DrawCircle(new Vector2(4, bob - 13), 1.2f, new Color(1f, 0.95f, 0.8f));
    }
}
