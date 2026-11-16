using Godot;

public enum FState { Idle, Walk, Attack, Jump, Hurt, Airborne, Down, GetUp, Dead, Charge }

/// <summary>Couleurs et taille d'un personnage (tout est dessiné en code).</summary>
public struct Appearance
{
    public Color Skin, Hair, Shirt, Pants, Gloves, Shoes;
    public float Size;
}

/// <summary>Position des mains / pieds pour une frame, en coordonnées locales (face à droite).</summary>
public struct Pose
{
    public Vector2 FrontHand, BackHand, FrontFoot, BackFoot;
    public float Lean;     // radians, positif = penché vers l'avant
    public bool Lying;     // au sol
    public bool GlowFist;  // poing doré lumineux
}

/// <summary>
/// Base commune du héros et des voyous : physique (saut, projection), réactions aux coups
/// et rendu procédural du personnage.
/// La position du nœud = les pieds au sol. Z = hauteur (saut / projection).
/// </summary>
public abstract partial class Fighter : Node2D
{
    public const float Gravity = 1900f;
    protected const float GetUpTime = 0.35f;

    public float Z, VZ;
    public Vector2 Vel;
    public int Facing = 1;
    public float Health = 100, MaxHealth = 100;
    public float Invuln;
    public string DisplayName = "";

    public FState State { get; private set; } = FState.Idle;
    public float StateTime { get; private set; }

    protected Appearance Look;
    protected float WalkPhase;
    protected float AnimTime;

    public float BodyScale => Look.Size;
    public float BodyRadius => 12f * Look.Size;
    public bool IsDead => State == FState.Dead;
    public bool IsDown => State is FState.Down or FState.Airborne or FState.GetUp or FState.Dead;
    public virtual bool CanBeHit => Invuln <= 0 && !IsDown;
    protected virtual float HurtDuration => 0.28f;
    protected virtual float ArmWidth => 7f;

    protected static Vector2 V(float x, float y) => new(x, y);

    protected void SetState(FState s)
    {
        State = s;
        StateTime = 0;
        OnStateChanged(s);
    }

    protected virtual void OnStateChanged(FState s) { }
    protected abstract void Tick(float dt);
    protected virtual void OnDeathFinished() { }
    protected virtual Color Tint => Colors.White;

    public override void _Process(double delta)
    {
        if (!Game.Instance.Frozen)
        {
            float dt = (float)delta;
            StateTime += dt;
            AnimTime += dt;
            if (Invuln > 0) Invuln -= dt;
            if (!UpdateReactions(dt)) Tick(dt);
            ConstrainPosition();
        }
        UpdateModulate();
        QueueRedraw();
    }

    protected virtual void ConstrainPosition()
    {
        Position = new Vector2(Position.X, Mathf.Clamp(Position.Y, Game.StreetTop, Game.StreetBottom));
    }

    private void UpdateModulate()
    {
        if (State == FState.Hurt && StateTime < 0.08f)
            Modulate = new Color(2.5f, 2.5f, 2.5f);
        else if (State == FState.Dead)
            Modulate = new Color(1, 1, 1, (int)(StateTime * 14) % 2 == 0 ? 0.15f : 1f);
        else if (Invuln > 0 && (int)(AnimTime * 20) % 2 == 0)
            Modulate = new Color(1, 1, 1, 0.35f);
        else
            Modulate = Tint;
    }

    /// <summary>Gère les états passifs (touché, projeté, au sol). Retourne true si le perso ne contrôle rien.</summary>
    private bool UpdateReactions(float dt)
    {
        switch (State)
        {
            case FState.Hurt:
                Position += Vel * dt;
                Vel = Vel.MoveToward(Vector2.Zero, 700 * dt);
                if (StateTime > HurtDuration) SetState(FState.Idle);
                return true;

            case FState.Airborne:
                Position += Vel * dt;
                VZ -= Gravity * dt;
                Z += VZ * dt;
                if (Z <= 0)
                {
                    Z = 0;
                    VZ = 0;
                    Vel = Vector2.Zero;
                    SetState(FState.Down);
                    Game.Instance.Shake(3);
                }
                return true;

            case FState.Down:
                if (StateTime > 0.9f) SetState(Health <= 0 ? FState.Dead : FState.GetUp);
                return true;

            case FState.GetUp:
                if (StateTime > GetUpTime)
                {
                    SetState(FState.Idle);
                    Invuln = 0.5f;
                }
                return true;

            case FState.Dead:
                if (StateTime > 1.0f) OnDeathFinished();
                return true;
        }
        return false;
    }

    /// <summary>Reçoit un coup venant de la direction dirX (+1 = poussé vers la droite).</summary>
    public virtual bool TakeHit(float damage, int dirX, bool knockdown)
    {
        if (!CanBeHit) return false;

        Health = Mathf.Max(0, Health - damage);
        Facing = -dirX;

        if (Health <= 0 || knockdown || Z > 5)
        {
            SetState(FState.Airborne);
            Vel = new Vector2(dirX * 240, 0);
            VZ = 430;
            Z = Mathf.Max(Z, 1);
        }
        else
        {
            SetState(FState.Hurt);
            Vel = new Vector2(dirX * 140, 0);
        }
        return true;
    }

    // ---------------------------------------------------------------- Rendu

    protected virtual Pose BuildPose()
    {
        float bob = Mathf.Sin(AnimTime * 4f) * 1.5f;
        var p = new Pose
        {
            FrontHand = V(17, -63 + bob),
            BackHand = V(9, -61 + bob),
            FrontFoot = V(10, 0),
            BackFoot = V(-9, 0),
        };

        switch (State)
        {
            case FState.Walk:
                float s = Mathf.Sin(WalkPhase);
                p.FrontFoot = V(4 + s * 11, -Mathf.Max(0, s) * 4);
                p.BackFoot = V(-4 - s * 11, -Mathf.Max(0, -s) * 4);
                p.FrontHand += V(0, s * 2);
                p.BackHand -= V(0, s * 2);
                break;
            case FState.Jump:
                p.FrontFoot = V(10, -16);
                p.BackFoot = V(-8, -12);
                p.FrontHand = V(14, -76);
                p.BackHand = V(-6, -74);
                break;
            case FState.Hurt:
                p.Lean = -0.3f;
                p.FrontHand = V(-2, -52);
                p.BackHand = V(-12, -56);
                p.FrontFoot = V(6, 0);
                p.BackFoot = V(-12, 0);
                break;
            case FState.Airborne:
                p.Lean = -0.9f;
                p.FrontHand = V(10, -88);
                p.BackHand = V(-14, -86);
                p.FrontFoot = V(14, -6);
                p.BackFoot = V(4, -10);
                break;
            case FState.Down:
            case FState.Dead:
                p.Lying = true;
                p.FrontHand = V(8, -46);
                p.BackHand = V(-16, -60);
                p.FrontFoot = V(8, 0);
                p.BackFoot = V(-6, 0);
                break;
            case FState.GetUp:
                p.Lean = -Mathf.Pi / 2 * (1 - Mathf.Clamp(StateTime / GetUpTime, 0, 1));
                p.FrontHand = V(14, -40);
                p.BackHand = V(-4, -40);
                break;
        }
        return p;
    }

    public override void _Draw()
    {
        var pose = BuildPose();
        float s = Look.Size;

        // Ombre au sol (rétrécit avec la hauteur)
        float sh = Mathf.Clamp(1f - Z / 250f, 0.4f, 1f);
        DrawSetTransformMatrix(new Transform2D(0, V(s * sh, s * 0.28f * sh), 0, Vector2.Zero));
        DrawCircle(Vector2.Zero, pose.Lying ? 42 : 22, new Color(0, 0, 0, 0.35f));

        float rot = (pose.Lying ? -Mathf.Pi / 2 : pose.Lean) * Facing;
        var origin = V(0, -Z - (pose.Lying ? 10 * s : 0));
        DrawSetTransformMatrix(new Transform2D(rot, V(Facing * s, s), 0, origin));
        DrawFigure(pose);
    }

    private void DrawFigure(Pose p)
    {
        var L = Look;
        var hip = V(0, -38);

        // Jambe arrière + bras arrière (plus sombres)
        DrawLeg(hip + V(-3, 0), p.BackFoot, L.Pants.Darkened(0.3f), L.Shoes.Darkened(0.3f));
        DrawArm(V(-3, -66), p.BackHand, L.Skin.Darkened(0.25f), L.Gloves.Darkened(0.25f), p.GlowFist);

        DrawLeg(hip + V(3, 0), p.FrontFoot, L.Pants, L.Shoes);

        // Torse
        DrawColoredPolygon(new[] { V(-12, -36), V(12, -36), V(14, -72), V(-14, -72) }, L.Shirt);
        DrawRect(new Rect2(-12, -40, 24, 4), L.Pants.Darkened(0.5f));
        DrawTorsoDetails();

        DrawHead(V(1, -83));
        DrawArm(V(5, -66), p.FrontHand, L.Skin, L.Gloves, p.GlowFist);
    }

    private void DrawLeg(Vector2 hip, Vector2 foot, Color c, Color shoe)
    {
        var knee = (hip + foot) / 2 + V(4, 0);
        DrawLine(hip, knee, c, 10);
        DrawLine(knee, foot, c, 9);
        DrawCircle(knee, 4.5f, c);
        DrawRect(new Rect2(foot.X - 4, foot.Y - 5, 13, 6), shoe);
    }

    private void DrawArm(Vector2 shoulder, Vector2 hand, Color skin, Color glove, bool glow)
    {
        var elbow = (shoulder + hand) / 2 + V(-3, 5);
        DrawLine(shoulder, elbow, skin, ArmWidth);
        DrawLine(elbow, hand, skin, ArmWidth - 1);
        DrawCircle(elbow, ArmWidth / 2, skin);
        if (glow)
        {
            float pulse = 1f + 0.2f * Mathf.Sin(AnimTime * 30);
            DrawCircle(hand, 13 * pulse, new Color(1f, 0.85f, 0.2f, 0.35f));
        }
        DrawCircle(hand, 6, glove);
    }

    protected virtual void DrawTorsoDetails() { }

    protected virtual void DrawHead(Vector2 c)
    {
        DrawCircle(c, 11, Look.Skin);
        DrawRect(new Rect2(c.X + 5, c.Y - 2, 3, 3), new Color(0.1f, 0.1f, 0.1f));
    }
}
