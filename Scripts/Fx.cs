using System.Collections.Generic;
using Godot;

/// <summary>Effets visuels : étincelles d'impact, textes flottants, ondes de choc.</summary>
public partial class Fx : Node2D
{
    private sealed class Spark { public Vector2 Pos; public float Life, Max, Size, Rot; public Color Color; }
    private sealed class Dot { public Vector2 Pos, Vel; public float Life, Max; public Color Color; }
    private sealed class FloatText { public Vector2 Pos; public float Life, Max; public int Size; public string Text = ""; public Color Color; }
    private sealed class Ring { public Vector2 Pos; public float Life, Max; public Color Color; }

    private readonly List<Spark> _sparks = new();
    private readonly List<Dot> _dots = new();
    private readonly List<FloatText> _texts = new();
    private readonly List<Ring> _rings = new();

    public void Hit(Vector2 pos, Color color, bool big)
    {
        _sparks.Add(new Spark { Pos = pos, Life = 0.18f, Max = 0.18f, Size = big ? 34 : 22, Rot = GD.Randf() * Mathf.Tau, Color = color });
        int n = big ? 14 : 6;
        for (int i = 0; i < n; i++)
        {
            float life = (float)GD.RandRange(0.2, 0.5);
            _dots.Add(new Dot
            {
                Pos = pos,
                Vel = Vector2.FromAngle(GD.Randf() * Mathf.Tau) * (float)GD.RandRange(120, 360),
                Life = life, Max = life, Color = color,
            });
        }
    }

    public void Text(Vector2 pos, string text, Color color, int size = 28) =>
        _texts.Add(new FloatText { Pos = pos, Life = 1f, Max = 1f, Size = size, Text = text, Color = color });

    public void Shockwave(Vector2 pos, Color color) =>
        _rings.Add(new Ring { Pos = pos, Life = 0.5f, Max = 0.5f, Color = color });

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _sparks.RemoveAll(s => (s.Life -= dt) <= 0);
        _rings.RemoveAll(r => (r.Life -= dt) <= 0);
        _texts.RemoveAll(t => (t.Life -= dt) <= 0);
        foreach (var t in _texts) t.Pos += new Vector2(0, -40 * dt);
        foreach (var d in _dots)
        {
            d.Life -= dt;
            d.Pos += d.Vel * dt;
            d.Vel *= 1f - 4f * dt;
        }
        _dots.RemoveAll(d => d.Life <= 0);
        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach (var r in _rings)
        {
            float k = 1 - r.Life / r.Max;
            DrawSetTransformMatrix(new Transform2D(0, new Vector2(1, 0.4f), 0, r.Pos));
            var c = r.Color;
            c.A = 1 - k;
            DrawArc(Vector2.Zero, 30 + 300 * k, 0, Mathf.Tau, 48, c, 10 * (1 - k) + 2);
            DrawArc(Vector2.Zero, 15 + 200 * k, 0, Mathf.Tau, 48, new Color(1, 1, 1, (1 - k) * 0.6f), 4);
        }
        DrawSetTransformMatrix(Transform2D.Identity);

        foreach (var s in _sparks)
        {
            float k = s.Life / s.Max;
            float grow = s.Size * (1.3f - k * 0.5f);
            var c = s.Color;
            c.A = k;
            for (int i = 0; i < 8; i++)
            {
                var dir = Vector2.FromAngle(s.Rot + i * Mathf.Tau / 8);
                float len = grow * (i % 2 == 0 ? 1f : 0.5f);
                DrawLine(s.Pos + dir * 4, s.Pos + dir * len, c, 3);
            }
            DrawCircle(s.Pos, s.Size * 0.3f * k, new Color(1, 1, 1, k));
        }

        foreach (var d in _dots)
        {
            var c = d.Color;
            c.A = d.Life / d.Max;
            DrawCircle(d.Pos, 2.5f, c);
        }

        var font = ThemeDB.FallbackFont;
        foreach (var t in _texts)
        {
            var c = t.Color;
            c.A = Mathf.Clamp(t.Life / t.Max * 2, 0, 1);
            var pos = t.Pos - new Vector2(150, 0);
            DrawStringOutline(font, pos, t.Text, HorizontalAlignment.Center, 300, t.Size, 8, new Color(0, 0, 0, c.A));
            DrawString(font, pos, t.Text, HorizontalAlignment.Center, 300, t.Size, c);
        }
    }
}
