using System.Collections.Generic;
using Godot;

/// <summary>
/// Chef d'orchestre : construit la scène, gère la caméra (défilement + verrouillage pendant les vagues),
/// les vagues de voyous, le score, les vies et les états du jeu.
/// </summary>
public partial class Game : Node2D
{
    public static Game Instance { get; private set; } = null!;

    public const float StreetTop = 322f, StreetBottom = 525f, StageLength = 4200f, HalfW = 480f, ScreenH = 540f;
    public static readonly Color GoldColor = new(1f, 0.82f, 0.18f);

    public enum Mode { Title, Playing, Paused, GameOver, Victory }

    private sealed record Wave(float TriggerX, string Banner, (ThugKind Kind, float Delay)[] Spawns);

    public Mode State { get; private set; } = Mode.Title;
    public bool Frozen => State != Mode.Playing || _hitStop > 0;

    public Hero Hero { get; private set; } = null!;
    public Fx Fx { get; private set; } = null!;
    public float CamX { get; private set; } = HalfW;
    public readonly List<Thug> Enemies = new();

    public int Score { get; private set; }
    public int Lives { get; private set; } = 2;
    public Thug? LastTarget { get; private set; }
    public float LastTargetTimer { get; private set; }
    public string Banner { get; private set; } = "";
    public float BannerTimer { get; private set; }
    public float GoTimer { get; private set; }
    public float FlashAlpha { get; private set; }
    public int WaveNumber => Mathf.Min(_waveIndex + 1, _waves.Count);
    public int WaveCount => _waves.Count;

    private Node2D _world = null!;
    private Camera2D _cam = null!;
    private readonly List<Pickup> _pickups = new();
    private readonly RandomNumberGenerator _rng = new();
    private List<Wave> _waves = new();
    private int _waveIndex, _spawnIndex;
    private bool _locked;
    private float _spawnTimer, _hitStop, _shake, _victoryTimer = -1;

    public override void _Ready()
    {
        Instance = this;
        _rng.Randomize();

        AddChild(new Stage { ZIndex = -100 });
        _world = new Node2D { YSortEnabled = true };
        AddChild(_world);

        Hero = new Hero { Position = new Vector2(180, 430) };
        _world.AddChild(Hero);

        Fx = new Fx { ZIndex = 100 };
        AddChild(Fx);

        _cam = new Camera2D { Position = new Vector2(CamX, ScreenH / 2) };
        AddChild(_cam);
        _cam.MakeCurrent();

        var hudLayer = new CanvasLayer();
        AddChild(hudLayer);
        hudLayer.AddChild(new Hud());

        _waves = new List<Wave>
        {
            new(700, "", new[] { (ThugKind.Punk, 0.2f), (ThugKind.Punk, 0.6f), (ThugKind.Punk, 2.5f) }),
            new(1600, "", new[] { (ThugKind.Punk, 0f), (ThugKind.Punk, 0.5f), (ThugKind.Brute, 2f), (ThugKind.Punk, 3f) }),
            new(2550, "", new[] { (ThugKind.Brute, 0f), (ThugKind.Punk, 0.5f), (ThugKind.Punk, 1f), (ThugKind.Brute, 3f), (ThugKind.Punk, 2f) }),
            new(3600, "LE CAÏD DÉBARQUE !", new[] { (ThugKind.Boss, 0.5f), (ThugKind.Punk, 1.5f), (ThugKind.Punk, 6f), (ThugKind.Brute, 6f) }),
        };
    }

    // ---------------------------------------------------------------- Entrées globales

    public override void _UnhandledInput(InputEvent e)
    {
        bool confirm = e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Enter or Key.KpEnter }
                       || e is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.Start };
        bool pause = e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape or Key.P }
                     || e is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.Start };

        switch (State)
        {
            case Mode.Title when confirm:
                State = Mode.Playing;
                ShowBanner("LIBÈRE LA VILLE !");
                break;
            case Mode.Title when e is InputEventKey { Pressed: true, Keycode: Key.Escape }:
                GetTree().Quit();
                break;
            case Mode.Playing when pause:
                State = Mode.Paused;
                break;
            case Mode.Paused when pause || confirm:
                State = Mode.Playing;
                break;
            case Mode.GameOver or Mode.Victory when confirm:
                GetTree().ReloadCurrentScene();
                break;
        }
    }

    // ---------------------------------------------------------------- Boucle

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _shake = Mathf.MoveToward(_shake, 0, dt * 25);
        FlashAlpha = Mathf.Max(0, FlashAlpha - dt * 2.5f);
        _cam.Offset = _shake > 0 ? new Vector2(_rng.RandfRange(-_shake, _shake), _rng.RandfRange(-_shake, _shake)) : Vector2.Zero;

        if (State != Mode.Playing) return;
        if (_hitStop > 0)
        {
            _hitStop -= dt;
            return;
        }

        BannerTimer = Mathf.Max(0, BannerTimer - dt);
        GoTimer = Mathf.Max(0, GoTimer - dt);
        LastTargetTimer = Mathf.Max(0, LastTargetTimer - dt);

        UpdateCamera(dt);
        UpdateWaves(dt);
        UpdatePickups();

        if (_victoryTimer >= 0)
        {
            _victoryTimer -= dt;
            if (_victoryTimer < 0) State = Mode.Victory;
        }
    }

    private void UpdateCamera(float dt)
    {
        if (!_locked)
        {
            float target = Mathf.Min(Mathf.Max(CamX, Hero.Position.X), StageLength - HalfW);
            CamX = Mathf.MoveToward(CamX, target, 500 * dt);

            if (_waveIndex < _waves.Count && CamX >= _waves[_waveIndex].TriggerX)
            {
                CamX = _waves[_waveIndex].TriggerX;
                _locked = true;
                _spawnIndex = 0;
                _spawnTimer = 0;
                GoTimer = 0;
                if (_waves[_waveIndex].Banner != "") ShowBanner(_waves[_waveIndex].Banner);
            }
        }
        _cam.Position = new Vector2(CamX, ScreenH / 2);
    }

    private void UpdateWaves(float dt)
    {
        if (!_locked) return;
        var wave = _waves[_waveIndex];

        if (_spawnIndex < wave.Spawns.Length)
        {
            _spawnTimer += dt;
            var next = wave.Spawns[_spawnIndex];
            if (_spawnTimer >= next.Delay && Enemies.Count < 5)
            {
                SpawnThug(next.Kind);
                _spawnIndex++;
                _spawnTimer = 0;
            }
        }
        else if (Enemies.Count == 0)
        {
            _locked = false;
            _waveIndex++;
            if (_waveIndex >= _waves.Count)
            {
                ShowBanner("LA VILLE EST LIBÉRÉE !");
                _victoryTimer = 2.5f;
            }
            else
            {
                GoTimer = 3f;
            }
        }
    }

    private void SpawnThug(ThugKind kind)
    {
        int side = kind == ThugKind.Boss ? 1 : (_rng.Randf() < 0.5f ? -1 : 1);
        var thug = new Thug
        {
            Position = new Vector2(CamX + side * (HalfW + 50), _rng.RandfRange(StreetTop + 20, StreetBottom - 10)),
        };
        thug.Setup(kind, _rng);
        _world.AddChild(thug);
        Enemies.Add(thug);
    }

    private void UpdatePickups()
    {
        for (int i = _pickups.Count - 1; i >= 0; i--)
        {
            var p = _pickups[i];
            if (Hero.IsDown || Hero.Z > 10) break;
            if (Mathf.Abs(p.Position.X - Hero.Position.X) < 30 && Mathf.Abs(p.Position.Y - Hero.Position.Y) < 18)
            {
                Hero.Health = Mathf.Min(Hero.MaxHealth, Hero.Health + p.Heal);
                Fx.Text(p.Position + new Vector2(0, -40), $"+{p.Heal} PV", new Color(0.4f, 1f, 0.4f), 26);
                Score += 50;
                _pickups.RemoveAt(i);
                p.QueueFree();
            }
        }
    }

    // ---------------------------------------------------------------- Évènements de combat

    public void HitStop(float t) => _hitStop = Mathf.Max(_hitStop, t);
    public void Shake(float amount) => _shake = Mathf.Max(_shake, amount);

    public void ShowBanner(string text)
    {
        Banner = text;
        BannerTimer = 2.5f;
    }

    public int EngagedCount()
    {
        int n = 0;
        foreach (var e in Enemies) if (e.Engaged) n++;
        return n;
    }

    public void OnHeroHit(Thug target, bool knockdown, bool golden)
    {
        var at = target.Position + new Vector2(-Hero.Facing * target.BodyRadius * 0.5f, -62 * target.BodyScale - target.Z);
        Fx.Hit(at, golden ? GoldColor : Colors.White, knockdown);
        HitStop(knockdown ? 0.09f : 0.045f);
        Score += knockdown ? 50 : 10;
        if (golden) Fx.Text(at + new Vector2(0, -30), "GOLDEN !", GoldColor, 30);
        if (knockdown) Shake(5);
        LastTarget = target;
        LastTargetTimer = 3f;
    }

    public void OnEnemyHitHero(bool knockdown)
    {
        Fx.Hit(Hero.Position + new Vector2(0, -62 - Hero.Z), new Color(1f, 0.35f, 0.3f), knockdown);
        HitStop(0.06f);
        Shake(knockdown ? 8 : 4);
    }

    public void SuperGolden(Hero hero)
    {
        FlashAlpha = 0.8f;
        Shake(14);
        HitStop(0.12f);
        Fx.Shockwave(hero.Position + new Vector2(hero.Facing * 30, 0), GoldColor);

        foreach (var e in Enemies)
        {
            float dx = e.Position.X - hero.Position.X;
            if (Mathf.Abs(dx) > 300 || Mathf.Abs(e.Position.Y - hero.Position.Y) > 140) continue;
            if (e.TakeHit(28, dx >= 0 ? 1 : -1, true))
            {
                Fx.Hit(e.Position + new Vector2(0, -60 * e.BodyScale), GoldColor, true);
                Score += 100;
                LastTarget = e;
                LastTargetTimer = 3f;
            }
        }
    }

    public void EnemyKilled(Thug thug)
    {
        Enemies.Remove(thug);
        Score += thug.ScoreValue;
        Fx.Text(thug.Position + new Vector2(0, -50), $"+{thug.ScoreValue}", GoldColor, 22);
        if (LastTarget == thug) LastTarget = null;

        if (thug.Kind != ThugKind.Boss && _rng.Randf() < 0.3f)
        {
            var p = new Pickup { Position = thug.Position };
            _world.AddChild(p);
            _pickups.Add(p);
        }
        thug.QueueFree();
    }

    public void HeroDied()
    {
        if (Lives <= 0)
        {
            State = Mode.GameOver;
            return;
        }

        Lives--;
        Hero.Respawn();
        FlashAlpha = 0.6f;
        // Comme dans les classiques du genre : le retour du héros repousse les voyous proches
        foreach (var e in Enemies)
            if (Mathf.Abs(e.Position.X - Hero.Position.X) < 320)
                e.TakeHit(0, e.Position.X >= Hero.Position.X ? 1 : -1, true);
    }
}
