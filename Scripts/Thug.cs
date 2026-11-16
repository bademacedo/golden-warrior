using Godot;

public enum ThugKind { Punk, Brute, Boss }

/// <summary>
/// Voyou : s'approche du héros, attend son tour (max 2 attaquants à la fois), télégraphie puis frappe.
/// Le Caïd (boss) peut en plus charger à travers l'écran.
/// </summary>
public partial class Thug : Fighter
{
    private static readonly string[] PunkNames = { "RICO", "DÉDÉ", "KÉVIN", "LULU", "ZACK", "MOMO", "TONY", "NINO" };
    private static readonly string[] BruteNames = { "BULLDOZER", "GROS BÉBERT", "LE TANK", "MASTOC" };
    private const float StrikeWindow = 0.12f, ChargeWindup = 0.55f, ChargeDuration = 0.8f, ChargeSpeed = 430f;

    public ThugKind Kind { get; private set; }
    public bool Engaged { get; private set; }
    public int ScoreValue { get; private set; }

    private float _speed, _damage, _reach, _windup, _recover, _kdChance;
    private float _attackCd, _chargeCd, _holdDist, _laneOffset;
    private bool _struck, _removed;

    protected override float HurtDuration => Kind == ThugKind.Boss ? 0.18f : 0.28f;
    protected override float ArmWidth => Kind == ThugKind.Punk ? 7f : 9f;

    protected override Color Tint =>
        State == FState.Attack && StateTime < _windup && Kind != ThugKind.Punk ? new Color(1.5f, 0.8f, 0.8f)
        : State == FState.Charge && StateTime < ChargeWindup ? new Color(1.6f, 0.7f, 0.7f)
        : Colors.White;

    public void Setup(ThugKind kind, RandomNumberGenerator rng)
    {
        Kind = kind;
        var skins = new[]
        {
            new Color(0.96f, 0.8f, 0.66f), new Color(0.82f, 0.62f, 0.45f),
            new Color(0.58f, 0.4f, 0.27f), new Color(0.38f, 0.26f, 0.18f),
        };
        var skin = skins[rng.RandiRange(0, skins.Length - 1)];
        var black = new Color(0.08f, 0.08f, 0.1f);

        switch (kind)
        {
            case ThugKind.Punk:
                var hairs = new[] { new Color(0.9f, 0.2f, 0.8f), new Color(0.3f, 0.9f, 0.3f), new Color(0.2f, 0.8f, 1f), new Color(1f, 0.5f, 0.1f) };
                Look = new Appearance
                {
                    Skin = skin, Hair = hairs[rng.RandiRange(0, hairs.Length - 1)],
                    Shirt = new Color(0.14f, 0.14f, 0.17f), Pants = new Color(0.35f, 0.38f, 0.48f),
                    Gloves = skin, Shoes = black, Size = 1f,
                };
                DisplayName = PunkNames[rng.RandiRange(0, PunkNames.Length - 1)];
                MaxHealth = 30; _speed = 100; _damage = 7; _reach = 50; _windup = 0.38f; _recover = 0.35f;
                _kdChance = 0; ScoreValue = 100;
                break;

            case ThugKind.Brute:
                Look = new Appearance
                {
                    Skin = skin, Hair = new Color(0.15f, 0.1f, 0.08f),
                    Shirt = new Color(0.75f, 0.15f, 0.15f), Pants = new Color(0.36f, 0.26f, 0.16f),
                    Gloves = new Color(0.25f, 0.25f, 0.25f), Shoes = black, Size = 1.22f,
                };
                DisplayName = BruteNames[rng.RandiRange(0, BruteNames.Length - 1)];
                MaxHealth = 70; _speed = 68; _damage = 13; _reach = 58; _windup = 0.55f; _recover = 0.5f;
                _kdChance = 0.4f; ScoreValue = 300;
                break;

            default:
                Look = new Appearance
                {
                    Skin = skin, Hair = black,
                    Shirt = new Color(0.45f, 0.15f, 0.55f), Pants = new Color(0.33f, 0.1f, 0.4f),
                    Gloves = new Color(0.92f, 0.92f, 0.92f), Shoes = black, Size = 1.32f,
                };
                DisplayName = "LE CAÏD";
                MaxHealth = 280; _speed = 88; _damage = 14; _reach = 62; _windup = 0.4f; _recover = 0.4f;
                _kdChance = 0.35f; ScoreValue = 5000;
                break;
        }

        Health = MaxHealth;
        _attackCd = rng.RandfRange(0.5f, 1.5f);
        _chargeCd = 3f;
        _holdDist = rng.RandfRange(130, 200);
        _laneOffset = rng.RandfRange(-50, 50);
    }

    protected override void OnStateChanged(FState s)
    {
        if (s is FState.Hurt or FState.Airborne or FState.Dead)
        {
            Engaged = false;
            _attackCd = Mathf.Max(_attackCd, 0.6f);
        }
    }

    protected override void Tick(float dt)
    {
        var hero = Game.Instance.Hero;
        switch (State)
        {
            case FState.Idle:
            case FState.Walk:
                Think(dt, hero);
                break;
            case FState.Attack:
                DoAttack(hero);
                break;
            case FState.Charge:
                DoCharge(dt, hero);
                break;
        }
    }

    private void Think(float dt, Hero hero)
    {
        var game = Game.Instance;
        _attackCd -= dt;
        _chargeCd -= dt;

        bool heroTargetable = !hero.IsDown;
        int maxAttackers = Kind == ThugKind.Boss ? 3 : 2;
        if (!heroTargetable) Engaged = false;
        else if (!Engaged && _attackCd <= 0 && game.EngagedCount() < maxAttackers) Engaged = true;

        float dyHero = Mathf.Abs(hero.Position.Y - Position.Y);
        float dxHero = Mathf.Abs(hero.Position.X - Position.X);
        Facing = hero.Position.X >= Position.X ? 1 : -1;

        // Le Caïd : charge s'il est aligné avec le héros
        if (Kind == ThugKind.Boss && heroTargetable && _chargeCd <= 0 && dyHero < 30 && dxHero > 180)
        {
            _struck = false;
            SetState(FState.Charge);
            game.Fx.Text(Position + V(0, -150), "!", new Color(1f, 0.3f, 0.3f), 48);
            return;
        }

        int side = Position.X >= hero.Position.X ? 1 : -1;
        Vector2 target = Engaged
            ? hero.Position + V(side * (_reach * 0.75f + hero.BodyRadius), 0)
            : hero.Position + V(side * _holdDist, _laneOffset);
        // Toujours rester atteignable : la cible reste dans l'écran
        target.X = Mathf.Clamp(target.X, game.CamX - Game.HalfW + 40, game.CamX + Game.HalfW - 40);

        var d = target - Position;
        if (d.Length() > 5)
        {
            float mx = Mathf.Sign(d.X) * Mathf.Min(Mathf.Abs(d.X), _speed * dt);
            float my = Mathf.Sign(d.Y) * Mathf.Min(Mathf.Abs(d.Y), _speed * 0.7f * dt);
            Position += V(mx, my);
            WalkPhase += dt * 10;
            if (State != FState.Walk) SetState(FState.Walk);
        }
        else if (State != FState.Idle)
        {
            SetState(FState.Idle);
        }

        if (Engaged && dxHero <= _reach + hero.BodyRadius && dyHero < 14)
        {
            _struck = false;
            SetState(FState.Attack);
        }
    }

    private void DoAttack(Hero hero)
    {
        if (!_struck && StateTime >= _windup && StateTime < _windup + StrikeWindow)
        {
            float dx = (hero.Position.X - Position.X) * Facing;
            if (dx > -5 && dx < _reach + hero.BodyRadius + 6 && Mathf.Abs(hero.Position.Y - Position.Y) < 20 && hero.Z < 40)
            {
                bool kd = GD.Randf() < _kdChance;
                if (hero.TakeHit(_damage, Facing, kd))
                {
                    _struck = true;
                    Game.Instance.OnEnemyHitHero(kd);
                }
            }
        }

        if (StateTime >= _windup + StrikeWindow + _recover)
        {
            Engaged = false;
            _attackCd = Kind == ThugKind.Brute ? (float)GD.RandRange(1.5, 2.8) : (float)GD.RandRange(1.0, 2.2);
            SetState(FState.Idle);
        }
    }

    private void DoCharge(float dt, Hero hero)
    {
        if (StateTime < ChargeWindup) return;

        var game = Game.Instance;
        float x = Mathf.Clamp(Position.X + Facing * ChargeSpeed * dt, game.CamX - Game.HalfW + 30, game.CamX + Game.HalfW - 30);
        Position = V(x, Position.Y);
        WalkPhase += dt * 20;

        if (!_struck && Mathf.Abs(hero.Position.X - x) < 44 && Mathf.Abs(hero.Position.Y - Position.Y) < 24 && hero.Z < 40
            && hero.TakeHit(16, Facing, true))
        {
            _struck = true;
            game.OnEnemyHitHero(true);
        }

        if (StateTime > ChargeWindup + ChargeDuration)
        {
            _chargeCd = (float)GD.RandRange(4.0, 7.0);
            SetState(FState.Idle);
        }
    }

    protected override void OnDeathFinished()
    {
        if (_removed) return;
        _removed = true;
        Game.Instance.EnemyKilled(this);
    }

    // ---------------------------------------------------------------- Rendu

    protected override Pose BuildPose()
    {
        var p = base.BuildPose();
        float s = Look.Size;
        var strike = V(_reach / s + 4, -64);

        if (State == FState.Attack)
        {
            float t = StateTime;
            p.FrontFoot = V(14, 0);
            p.BackFoot = V(-12, 0);
            if (t < _windup)
            {
                float k = t / _windup;
                p.FrontHand = V(17 - 24 * k, -63 - 4 * k);
                p.Lean = -0.14f * k;
            }
            else if (t < _windup + StrikeWindow)
            {
                p.FrontHand = strike;
                p.Lean = 0.15f;
            }
            else
            {
                float k = Mathf.Clamp((t - _windup - StrikeWindow) / _recover, 0, 1);
                p.FrontHand = strike.Lerp(V(17, -63), k);
                p.Lean = 0.15f * (1 - k);
            }
        }
        else if (State == FState.Charge)
        {
            if (StateTime < ChargeWindup)
            {
                p.Lean = -0.15f;
                p.FrontHand = V(-4, -60 + Mathf.Sin(AnimTime * 40) * 2);
            }
            else
            {
                float sw = Mathf.Sin(WalkPhase);
                p.Lean = 0.4f;
                p.FrontHand = V(12, -50);
                p.BackHand = V(-10, -50);
                p.FrontFoot = V(6 + sw * 14, -Mathf.Max(0, sw) * 5);
                p.BackFoot = V(-6 - sw * 14, -Mathf.Max(0, -sw) * 5);
            }
        }
        return p;
    }

    protected override void DrawTorsoDetails()
    {
        switch (Kind)
        {
            case ThugKind.Punk:
                // Veste en cuir ouverte sur un t-shirt gris
                DrawColoredPolygon(new[] { V(-2, -38), V(5, -38), V(6, -72), V(-3, -72) }, new Color(0.55f, 0.55f, 0.58f));
                DrawCircle(V(-8, -64), 1.5f, new Color(0.8f, 0.8f, 0.8f));
                DrawCircle(V(-8, -56), 1.5f, new Color(0.8f, 0.8f, 0.8f));
                break;
            case ThugKind.Brute:
                // Débardeur : épaules nues
                DrawCircle(V(-10, -68), 5, Look.Skin.Darkened(0.1f));
                DrawCircle(V(10, -68), 5, Look.Skin.Darkened(0.1f));
                break;
            default:
                // Costume : chemise blanche + cravate rouge
                DrawColoredPolygon(new[] { V(-5, -72), V(7, -72), V(1, -52) }, new Color(0.95f, 0.95f, 0.95f));
                DrawColoredPolygon(new[] { V(-1, -70), V(3, -70), V(4, -54), V(1, -50), V(-2, -54) }, new Color(0.8f, 0.1f, 0.1f));
                break;
        }
    }

    protected override void DrawHead(Vector2 c)
    {
        var dark = new Color(0.1f, 0.1f, 0.1f);
        switch (Kind)
        {
            case ThugKind.Punk:
                DrawColoredPolygon(new[] { c + V(-5, -8), c + V(-9, -20), c + V(-3, -15), c + V(0, -27), c + V(3, -15), c + V(9, -21), c + V(5, -8) }, Look.Hair);
                DrawCircle(c, 11, Look.Skin);
                DrawRect(new Rect2(c.X + 5, c.Y - 2, 3, 3), dark);
                DrawLine(c + V(3, -5), c + V(9, -3), dark, 2);
                DrawLine(c + V(3, 5), c + V(8, 6), dark, 1.5f);
                break;

            case ThugKind.Brute:
                DrawCircle(c, 12, Look.Skin);
                DrawCircle(c + V(-2, -6), 5, Look.Skin.Lightened(0.2f));
                DrawColoredPolygon(new[] { c + V(-4, 2), c + V(12, 2), c + V(9, 12), c + V(0, 13) }, Look.Hair);
                DrawRect(new Rect2(c.X + 5, c.Y - 3, 3, 3), dark);
                DrawLine(c + V(2, -6), c + V(10, -4), dark, 3);
                break;

            default:
                DrawCircle(c, 11, Look.Skin);
                DrawColoredPolygon(new[] { c + V(-12, 2), c + V(-11, -8), c + V(-2, -13), c + V(10, -10), c + V(11, -5), c + V(-4, -6), c + V(-8, 2) }, Look.Hair);
                DrawRect(new Rect2(c.X - 1, c.Y - 4, 12, 5), dark); // lunettes noires
                DrawLine(c + V(3, 5), c + V(10, 5), Look.Hair, 2.5f); // moustache
                break;
        }
    }
}
