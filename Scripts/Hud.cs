using Godot;

/// <summary>Interface : barres de vie, jauge dorée, score, bannières et écrans titre / pause / fin.</summary>
public partial class Hud : Node2D
{
    private const float W = 960, H = 540;
    private float _t;
    private Font _font = null!;

    public override void _Ready() => _font = ThemeDB.FallbackFont;

    public override void _Process(double delta)
    {
        _t += (float)delta;
        QueueRedraw();
    }

    private void Text(string s, Vector2 pos, int size, Color c, HorizontalAlignment align = HorizontalAlignment.Left, float width = -1, int outline = 6)
    {
        DrawStringOutline(_font, pos, s, align, width, size, outline, new Color(0, 0, 0, c.A));
        DrawString(_font, pos, s, align, width, size, c);
    }

    private void Bar(Rect2 r, float ratio, Color fill)
    {
        DrawRect(r.Grow(2), Colors.Black);
        DrawRect(r, new Color(0.35f, 0.05f, 0.05f));
        DrawRect(new Rect2(r.Position, new Vector2(r.Size.X * Mathf.Clamp(ratio, 0, 1), r.Size.Y)), fill);
    }

    public override void _Draw()
    {
        var g = Game.Instance;
        var gold = Game.GoldColor;
        float blink = 0.5f + 0.5f * Mathf.Sin(_t * 6);

        if (g.State != Game.Mode.Title)
        {
            DrawRect(new Rect2(0, 0, W, 70), new Color(0, 0, 0, 0.5f));

            // Héros
            Text("GOLDEN WARRIOR", new Vector2(16, 22), 16, gold, outline: 4);
            Text($"x{g.Lives}", new Vector2(290, 42), 20, Colors.White, outline: 4);
            Bar(new Rect2(16, 30, 260, 14), g.Hero.Health / g.Hero.MaxHealth, new Color(1f, 0.85f, 0.25f));

            float goldRatio = g.Hero.Gold / Hero.GoldMax;
            Bar(new Rect2(16, 52, 200, 8), goldRatio, goldRatio >= 1 ? gold.Lightened(0.4f * blink) : gold.Darkened(0.2f));
            if (goldRatio >= 1)
                Text("SUPER GOLDEN PRÊT ! [L]", new Vector2(226, 62), 14, new Color(1, 1, 1, 0.5f + 0.5f * blink), outline: 4);

            // Score / vague
            Text($"SCORE {g.Score:D7}", new Vector2(W - 316, 26), 22, Colors.White, HorizontalAlignment.Right, 300);
            Text($"ZONE {g.WaveNumber}/{g.WaveCount}", new Vector2(W - 316, 48), 14, new Color(1, 1, 1, 0.7f), HorizontalAlignment.Right, 300, 4);

            // Dernier voyou frappé
            if (g.LastTarget is { } t && g.LastTargetTimer > 0 && IsInstanceValid(t))
            {
                Text(t.DisplayName, new Vector2(460, 30), 16, new Color(1f, 0.5f, 0.5f), outline: 4);
                Bar(new Rect2(460, 38, 200, 10), t.Health / t.MaxHealth, new Color(0.9f, 0.25f, 0.25f));
            }

            if (g.BannerTimer > 0)
            {
                float a = Mathf.Clamp(g.BannerTimer * 2, 0, 1);
                Text(g.Banner, new Vector2(0, 160), 44, new Color(gold, a), HorizontalAlignment.Center, W, 10);
            }

            if (g.GoTimer > 0 && blink > 0.4f)
                Text("GO >>", new Vector2(W - 190, 260), 48, gold, outline: 10);
        }

        if (g.FlashAlpha > 0)
            DrawRect(new Rect2(0, 0, W, H), new Color(1, 0.95f, 0.8f, g.FlashAlpha));

        switch (g.State)
        {
            case Game.Mode.Title:
                DrawRect(new Rect2(0, 0, W, H), new Color(0, 0, 0, 0.6f));
                Text("GOLDEN WARRIOR", new Vector2(0, 150), 78, gold, HorizontalAlignment.Center, W, 14);
                Text("Libère la ville des voyous à coups de Goldens !", new Vector2(0, 200), 22, Colors.White, HorizontalAlignment.Center, W);
                Text("Déplacement : ZQSD / WASD / flèches", new Vector2(0, 270), 18, new Color(1, 1, 1, 0.8f), HorizontalAlignment.Center, W, 4);
                Text("J : Golden (coup de poing, 3 de suite = combo)   K : Saut   (saut + J = coup de pied)", new Vector2(0, 298), 18, new Color(1, 1, 1, 0.8f), HorizontalAlignment.Center, W, 4);
                Text("L : SUPER GOLDEN quand la jauge dorée est pleine     Échap : pause", new Vector2(0, 326), 18, new Color(1, 1, 1, 0.8f), HorizontalAlignment.Center, W, 4);
                Text("Manette : croix / stick, X = Golden, A = Saut, Y = Super", new Vector2(0, 354), 14, new Color(1, 1, 1, 0.5f), HorizontalAlignment.Center, W, 4);
                Text("Appuie sur ENTRÉE", new Vector2(0, 440), 30, new Color(1, 1, 1, 0.3f + 0.7f * blink), HorizontalAlignment.Center, W, 8);
                break;

            case Game.Mode.Paused:
                DrawRect(new Rect2(0, 0, W, H), new Color(0, 0, 0, 0.5f));
                Text("PAUSE", new Vector2(0, 270), 60, Colors.White, HorizontalAlignment.Center, W, 10);
                Text("Échap ou Entrée pour reprendre", new Vector2(0, 320), 20, new Color(1, 1, 1, 0.7f), HorizontalAlignment.Center, W, 4);
                break;

            case Game.Mode.GameOver:
                DrawRect(new Rect2(0, 0, W, H), new Color(0.2f, 0, 0, 0.6f));
                Text("GAME OVER", new Vector2(0, 240), 72, new Color(1f, 0.3f, 0.3f), HorizontalAlignment.Center, W, 12);
                Text("Les voyous ont gagné... pour cette fois.", new Vector2(0, 290), 22, Colors.White, HorizontalAlignment.Center, W);
                Text($"Score : {g.Score}", new Vector2(0, 340), 28, gold, HorizontalAlignment.Center, W);
                Text("ENTRÉE pour rejouer", new Vector2(0, 430), 26, new Color(1, 1, 1, 0.3f + 0.7f * blink), HorizontalAlignment.Center, W, 8);
                break;

            case Game.Mode.Victory:
                DrawRect(new Rect2(0, 0, W, H), new Color(0.15f, 0.1f, 0, 0.6f));
                Text("VICTOIRE !", new Vector2(0, 220), 78, gold, HorizontalAlignment.Center, W, 14);
                Text("Le Caïd est tombé. La ville respire grâce au Golden Warrior !", new Vector2(0, 275), 22, Colors.White, HorizontalAlignment.Center, W);
                Text($"Score final : {g.Score}", new Vector2(0, 330), 30, gold, HorizontalAlignment.Center, W);
                Text("ENTRÉE pour rejouer", new Vector2(0, 430), 26, new Color(1, 1, 1, 0.3f + 0.7f * blink), HorizontalAlignment.Center, W, 8);
                break;
        }
    }
}
