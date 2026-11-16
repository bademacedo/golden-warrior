using System.Collections.Generic;
using Godot;

/// <summary>Décor de la ville la nuit : ciel, immeubles lointains (parallaxe), façades, trottoir et rue.</summary>
public partial class Stage : Node2D
{
    private struct Building
    {
        public float X, W, H;
        public Color Color;
        public int Seed;
        public string Sign;
    }

    private const float Ground = 300f;
    private static readonly string[] Signs = { "BAR", "PIZZA", "HÔTEL", "GARAGE", "LAVERIE", "KEBAB", "CINÉ", "BOXE", "", "", "" };

    private readonly List<Building> _far = new();
    private readonly List<Building> _near = new();

    public override void _Ready()
    {
        var rng = new RandomNumberGenerator { Seed = 1337 };

        for (float x = -200; x < Game.StageLength; x += rng.RandfRange(50, 110))
        {
            _far.Add(new Building { X = x, W = rng.RandfRange(60, 120), H = rng.RandfRange(120, 230), Seed = rng.RandiRange(0, 9999) });
        }

        var bricks = new[]
        {
            new Color(0.35f, 0.18f, 0.16f), new Color(0.25f, 0.22f, 0.3f), new Color(0.3f, 0.27f, 0.22f),
            new Color(0.2f, 0.25f, 0.28f), new Color(0.4f, 0.28f, 0.2f),
        };
        for (float x = -300; x < Game.StageLength + 600;)
        {
            float w = rng.RandfRange(160, 280);
            _near.Add(new Building
            {
                X = x, W = w, H = rng.RandfRange(150, 250),
                Color = bricks[rng.RandiRange(0, bricks.Length - 1)],
                Seed = rng.RandiRange(0, 9999),
                Sign = Signs[rng.RandiRange(0, Signs.Length - 1)],
            });
            x += w + rng.RandfRange(0, 20);
        }
    }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        float cam = Game.Instance.CamX;
        float left = cam - Game.HalfW - 40, right = cam + Game.HalfW + 40, width = right - left;
        var font = ThemeDB.FallbackFont;

        // Ciel dégradé + lune
        var top = new Color(0.05f, 0.04f, 0.15f);
        var bottom = new Color(0.45f, 0.2f, 0.35f);
        for (int i = 0; i < 10; i++)
            DrawRect(new Rect2(left, i * 30, width, 31), top.Lerp(bottom, i / 9f));
        DrawCircle(new Vector2(cam + 280, 70), 30, new Color(1f, 0.95f, 0.8f, 0.9f));
        DrawCircle(new Vector2(cam + 280, 70), 45, new Color(1f, 0.95f, 0.8f, 0.1f));

        // Immeubles lointains (parallaxe : se déplacent à 60 % de la vitesse caméra)
        foreach (var b in _far)
        {
            float x = b.X + cam * 0.4f;
            if (x + b.W < left || x > right) continue;
            DrawRect(new Rect2(x, Ground - b.H, b.W, b.H), new Color(0.1f, 0.08f, 0.2f));
            for (int j = 0; j * 18 + 16 < b.H; j++)
                for (int i = 0; i * 14 + 12 < b.W; i++)
                    if ((b.Seed + i * 7 + j * 13) % 6 == 0)
                        DrawRect(new Rect2(x + 6 + i * 14, Ground - b.H + 10 + j * 18, 6, 8), new Color(1f, 0.85f, 0.4f, 0.5f));
        }

        // Façades
        foreach (var b in _near)
        {
            if (b.X + b.W < left || b.X > right) continue;
            DrawBuilding(b, font);
        }

        // Trottoir
        DrawRect(new Rect2(left, Ground, width, 36), new Color(0.45f, 0.44f, 0.47f));
        for (float x = Mathf.Floor(left / 64) * 64; x < right; x += 64)
            DrawLine(new Vector2(x, Ground), new Vector2(x - 12, Ground + 36), new Color(0.35f, 0.34f, 0.37f), 2);
        DrawRect(new Rect2(left, Ground + 34, width, 6), new Color(0.6f, 0.6f, 0.62f));

        // Rue
        DrawRect(new Rect2(left, Ground + 40, width, 220), new Color(0.17f, 0.17f, 0.2f));
        for (float x = Mathf.Floor(left / 140) * 140; x < right; x += 140)
            DrawRect(new Rect2(x, 445, 70, 5), new Color(0.85f, 0.8f, 0.5f, 0.6f));

        // Lampadaires
        for (float x = Mathf.Floor(left / 520) * 520 + 260; x < right; x += 520)
        {
            DrawColoredPolygon(new[] { new Vector2(x + 30, 150), new Vector2(x - 40, Ground + 120), new Vector2(x + 110, Ground + 120) },
                new Color(1f, 0.9f, 0.5f, 0.07f));
            DrawRect(new Rect2(x - 3, 150, 6, Ground + 8 - 150), new Color(0.15f, 0.15f, 0.18f));
            DrawRect(new Rect2(x - 3, 146, 40, 6), new Color(0.15f, 0.15f, 0.18f));
            DrawCircle(new Vector2(x + 32, 154), 7, new Color(1f, 0.92f, 0.6f));
        }

        // Fin du niveau : barrière
        DrawRect(new Rect2(Game.StageLength - 6, Ground - 60, 12, 300), new Color(0.6f, 0.5f, 0.1f));
    }

    private void DrawBuilding(Building b, Font font)
    {
        float top = Ground - b.H;
        DrawRect(new Rect2(b.X, top, b.W, b.H), b.Color);
        DrawRect(new Rect2(b.X - 4, top - 8, b.W + 8, 10), b.Color.Darkened(0.35f));

        // Fenêtres (au-dessus du rez-de-chaussée)
        for (int j = 0; top + 20 + j * 46 + 30 < Ground - 70; j++)
            for (int i = 0; b.X + 18 + i * 44 + 26 < b.X + b.W; i++)
            {
                bool lit = (b.Seed + i * 11 + j * 17) % 3 == 0;
                var r = new Rect2(b.X + 18 + i * 44, top + 20 + j * 46, 26, 30);
                DrawRect(r, lit ? new Color(1f, 0.85f, 0.45f) : new Color(0.08f, 0.08f, 0.14f));
                DrawRect(r, b.Color.Darkened(0.4f), false, 2);
            }

        // Porte
        float doorX = b.X + b.W * 0.15f;
        DrawRect(new Rect2(doorX, Ground - 50, 28, 50), b.Color.Darkened(0.6f));

        // Enseigne néon
        if (b.Sign != "")
        {
            var signRect = new Rect2(b.X + b.W * 0.4f, Ground - 66, b.W * 0.5f, 24);
            DrawRect(signRect, new Color(0.05f, 0.05f, 0.08f));
            var neon = new Color[] { new(1f, 0.3f, 0.6f), new(0.3f, 0.9f, 1f), new(1f, 0.8f, 0.2f) }[b.Seed % 3];
            DrawRect(signRect, neon, false, 2);
            DrawString(font, new Vector2(signRect.Position.X, signRect.Position.Y + 18), b.Sign,
                HorizontalAlignment.Center, signRect.Size.X, 16, neon);
        }
    }
}
