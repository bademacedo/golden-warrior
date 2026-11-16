using System.Collections.Generic;
using Godot;

/// <summary>
/// Le Golden Warrior. Combo de 3 coups (le 3e = "Golden" qui projette), coup de pied sauté,
/// et "Super Golden" (onde de choc) quand la jauge dorée est pleine.
/// </summary>
public partial class Hero : Fighter
{
    public const float GoldMax = 100f;
    private const float SpeedX = 230f, SpeedY = 150f, JumpSpeed = 640f;
    private const float KickDuration = 0.35f, SpecialDuration = 0.8f, SpecialHitTime = 0.3f;

    // Durée, début/fin de la fenêtre active, dégâts, portée, projection
    private static readonly (float Dur, float Start, float End, float Damage, float Reach, bool Knockdown)[] Combo =
    {
        (0.22f, 0.05f, 0.12f, 6f, 56f, false),  // jab
        (0.24f, 0.05f, 0.13f, 7f, 58f, false),  // direct
        (0.42f, 0.10f, 0.22f, 15f, 62f, true),  // GOLDEN (uppercut)
    };

    public float Gold { get; private set; }

    private int _step;
    private bool _queued, _special, _specialDone, _kick;
    private float _kickTime;
    private bool _prevAtk, _prevJump, _prevSpecial;
    private bool _atkPressed, _jumpPressed, _specialPressed;
    private readonly HashSet<Fighter> _hitThisAttack = new();

    public Hero()
    {
        DisplayName = "GOLDEN WARRIOR";
        MaxHealth = Health = 100;
        Look = new Appearance
        {
            Skin = new Color(0.95f, 0.78f, 0.62f),
            Hair = new Color(1f, 0.8f, 0.15f),
            Shirt = new Color(0.96f, 0.96f, 0.93f),
            Pants = new Color(0.18f, 0.27f, 0.55f),
            Gloves = new Color(1f, 0.78f, 0.1f),
            Shoes = new Color(0.6f, 0.12f, 0.12f),
            Size = 1.05f,
        };
    }

    public void Respawn()
    {
        Health = MaxHealth;
        Z = VZ = 0;
        Vel = Vector2.Zero;
        _special = _kick = false;
        SetState(FState.Idle);
        Invuln = 2.5f;
    }

    public void AddGold(float amount) => Gold = Mathf.Min(GoldMax, Gold + amount);

    // ---------------------------------------------------------------- Entrées

    private void ReadInput()
    {
        bool atk = Input.IsPhysicalKeyPressed(Key.J) || Input.IsJoyButtonPressed(0, JoyButton.X);
        bool jump = Input.IsPhysicalKeyPressed(Key.K) || Input.IsJoyButtonPressed(0, JoyButton.A);
        bool sp = Input.IsPhysicalKeyPressed(Key.L) || Input.IsJoyButtonPressed(0, JoyButton.Y);
        _atkPressed = atk && !_prevAtk;
        _jumpPressed = jump && !_prevJump;
        _specialPressed = sp && !_prevSpecial;
        _prevAtk = atk;
        _prevJump = jump;
        _prevSpecial = sp;
    }

    private static Vector2 MoveInput()
    {
        // Touches physiques : WASD en QWERTY = ZQSD en AZERTY
        var v = Vector2.Zero;
        if (Input.IsPhysicalKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left) || Input.IsJoyButtonPressed(0, JoyButton.DpadLeft)) v.X -= 1;
        if (Input.IsPhysicalKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right) || Input.IsJoyButtonPressed(0, JoyButton.DpadRight)) v.X += 1;
        if (Input.IsPhysicalKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up) || Input.IsJoyButtonPressed(0, JoyButton.DpadUp)) v.Y -= 1;
        if (Input.IsPhysicalKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down) || Input.IsJoyButtonPressed(0, JoyButton.DpadDown)) v.Y += 1;

        var stick = new Vector2(Input.GetJoyAxis(0, JoyAxis.LeftX), Input.GetJoyAxis(0, JoyAxis.LeftY));
        if (stick.Length() > 0.3f) v += stick;
        return v.LimitLength(1);
    }

    // ---------------------------------------------------------------- Logique

    protected override void Tick(float dt)
    {
        ReadInput();
        var move = MoveInput();

        switch (State)
        {
            case FState.Idle:
            case FState.Walk:
                if (_specialPressed && Gold >= GoldMax) { StartSpecial(); break; }
                if (_atkPressed) { StartAttack(0); break; }
                if (_jumpPressed)
                {
                    SetState(FState.Jump);
                    VZ = JumpSpeed;
                    Vel = new Vector2(move.X * SpeedX, move.Y * SpeedY * 0.8f);
                    _kick = false;
                    break;
                }
                if (move != Vector2.Zero)
                {
                    Position += new Vector2(move.X * SpeedX, move.Y * SpeedY) * dt;
                    if (Mathf.Abs(move.X) > 0.1f) Facing = move.X > 0 ? 1 : -1;
                    WalkPhase += dt * 11;
                    if (State != FState.Walk) SetState(FState.Walk);
                }
                else if (State != FState.Idle)
                {
                    SetState(FState.Idle);
                }
                break;

            case FState.Jump:
                Position += Vel * dt;
                VZ -= Gravity * dt;
                Z += VZ * dt;
                if (_atkPressed && !_kick)
                {
                    _kick = true;
                    _kickTime = 0;
                    _hitThisAttack.Clear();
                }
                if (_kick)
                {
                    _kickTime += dt;
                    if (_kickTime < KickDuration) TryHit(12, 64, true, false);
                }
                if (Z <= 0)
                {
                    Z = 0;
                    _kick = false;
                    SetState(FState.Idle);
                }
                break;

            case FState.Attack:
                UpdateAttack(dt);
                break;
        }
    }

    private void StartAttack(int step)
    {
        _step = step;
        _queued = false;
        _special = false;
        _hitThisAttack.Clear();
        SetState(FState.Attack);
    }

    private void StartSpecial()
    {
        Gold = 0;
        _special = true;
        _specialDone = false;
        Invuln = SpecialDuration + 0.2f;
        SetState(FState.Attack);
        Game.Instance.Fx.Text(Position + V(0, -130), "SUPER GOLDEN !", Game.GoldColor, 34);
    }

    private void UpdateAttack(float dt)
    {
        if (_special)
        {
            if (!_specialDone && StateTime >= SpecialHitTime)
            {
                _specialDone = true;
                Game.Instance.SuperGolden(this);
            }
            if (StateTime >= SpecialDuration) { _special = false; SetState(FState.Idle); }
            return;
        }

        var a = Combo[_step];
        if (_atkPressed && StateTime > 0.06f) _queued = true;

        if (StateTime < a.End) Position += new Vector2(Facing * (_step == 2 ? 70 : 25) * dt, 0);
        if (StateTime >= a.Start && StateTime <= a.End) TryHit(a.Damage, a.Reach, a.Knockdown, _step == 2);

        if (StateTime >= a.Dur)
        {
            if (_queued && _step < 2) StartAttack(_step + 1);
            else SetState(FState.Idle);
        }
    }

    private void TryHit(float damage, float reach, bool knockdown, bool golden)
    {
        var game = Game.Instance;
        foreach (var e in game.Enemies)
        {
            if (_hitThisAttack.Contains(e) || !e.CanBeHit) continue;
            float dx = (e.Position.X - Position.X) * Facing;
            if (dx < -8 || dx > reach + e.BodyRadius) continue;
            if (Mathf.Abs(e.Position.Y - Position.Y) > 22) continue;
            if (Mathf.Abs(Z - e.Z) > 70) continue;

            if (e.TakeHit(damage, Facing, knockdown))
            {
                _hitThisAttack.Add(e);
                AddGold(golden ? 10 : 5);
                game.OnHeroHit(e, knockdown, golden);
            }
        }
    }

    public override bool TakeHit(float damage, int dirX, bool knockdown)
    {
        if (!base.TakeHit(damage, dirX, knockdown)) return false;
        _special = false;
        _kick = false;
        return true;
    }

    protected override void ConstrainPosition()
    {
        base.ConstrainPosition();
        var g = Game.Instance;
        float x = Mathf.Clamp(Position.X, g.CamX - Game.HalfW + 24, g.CamX + Game.HalfW - 24);
        Position = new Vector2(x, Position.Y);
    }

    protected override void OnDeathFinished() => Game.Instance.HeroDied();

    // ---------------------------------------------------------------- Rendu

    protected override Pose BuildPose()
    {
        var p = base.BuildPose();

        if (State == FState.Attack && _special)
        {
            p.GlowFist = true;
            if (StateTime < SpecialHitTime)
            {
                p.FrontHand = V(10, -102);
                p.BackHand = V(-6, -100);
                p.Lean = -0.12f;
            }
            else
            {
                p.FrontHand = V(32, -8);
                p.BackHand = V(24, -10);
                p.Lean = 0.55f;
                p.BackFoot = V(-16, 0);
            }
        }
        else if (State == FState.Attack)
        {
            var a = Combo[_step];
            float t = StateTime;
            float ext = t < a.Start ? t / a.Start : t <= a.End ? 1 : 1 - (t - a.End) / (a.Dur - a.End);
            ext = Mathf.Clamp(ext, 0, 1);
            p.FrontFoot = V(14, 0);
            p.BackFoot = V(-12, 0);

            switch (_step)
            {
                case 0:
                    p.FrontHand = V(17, -63).Lerp(V(48, -65), ext);
                    break;
                case 1:
                    p.BackHand = V(9, -61).Lerp(V(50, -64), ext);
                    p.Lean = 0.15f * ext;
                    break;
                default:
                    p.GlowFist = true;
                    p.FrontHand = V(18, -40).Lerp(V(32, -104), ext);
                    p.Lean = -0.12f * ext;
                    p.FrontFoot = V(16, -4 * ext);
                    break;
            }
        }
        else if (State == FState.Jump && _kick && _kickTime < KickDuration)
        {
            p.FrontFoot = V(44, -30);
            p.BackFoot = V(-8, -14);
            p.Lean = -0.15f;
        }
        else if (Gold >= GoldMax && !IsDown)
        {
            p.GlowFist = true;
        }
        return p;
    }

    protected override void DrawTorsoDetails()
    {
        // Emblème doré "G" + ceinture dorée
        DrawCircle(V(1, -56), 6, Game.GoldColor);
        DrawCircle(V(1, -56), 3.5f, Look.Shirt);
        DrawRect(new Rect2(1, -57, 5, 2), Game.GoldColor);
        DrawRect(new Rect2(-12, -40, 24, 4), Game.GoldColor.Darkened(0.2f));
    }

    protected override void DrawHead(Vector2 c)
    {
        var hair = Look.Hair;
        var band = new Color(0.85f, 0.12f, 0.12f);

        // Mèches dorées en pointes
        DrawColoredPolygon(new[]
        {
            c + V(-12, -1), c + V(-19, -10), c + V(-10, -11), c + V(-12, -21), c + V(-2, -14),
            c + V(2, -24), c + V(7, -14), c + V(15, -16), c + V(12, -4),
        }, hair);
        DrawCircle(c, 11, Look.Skin);
        DrawColoredPolygon(new[] { c + V(-11, -3), c + V(-9, -10), c + V(0, -13), c + V(9, -10), c + V(11, -5), c + V(4, -7), c + V(-4, -6) }, hair);

        // Bandeau rouge flottant
        DrawRect(new Rect2(c.X - 11, c.Y - 6, 22, 4), band);
        float wave = Mathf.Sin(AnimTime * 10) * 3;
        DrawLine(c + V(-10, -4), c + V(-22, 0 + wave), band, 3);
        DrawLine(c + V(-10, -4), c + V(-20, 5 - wave), band, 2.5f);

        DrawRect(new Rect2(c.X + 5, c.Y - 2, 3, 3), new Color(0.1f, 0.1f, 0.1f));
        DrawLine(c + V(3, 5), c + V(8, 4), new Color(0.4f, 0.2f, 0.15f), 1.5f);
    }
}
