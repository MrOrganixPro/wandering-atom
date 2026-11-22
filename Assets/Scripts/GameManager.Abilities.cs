using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// The Excited Atom's Lab abilities (it brings the one equipped in the Lab):
///  - Electrodash: burns the outermost electron to overcharge and jump instantly a fixed
///    distance the way you aim, untouchable on the way and for a moment after.
///  - Electroblast: burns the outermost electron into a huge ray to the edge of the screen
///    that vaporizes every enemy in it and shoves dust and black holes away.
/// And the Alkali Metal character's (while an alkali metal, see GameManager.Traits): its outermost electron is thrown
/// at the aimed point, explodes there and comes back (AlkaliShot).
/// Used from the HUD button: time stops, the mouse aims (a preview shows what will happen),
/// a click fires, Esc / right click / the button again cancels without spending anything.
/// </summary>
public partial class GameManager
{
    [Header("Excited Atom abilities (Lab)")]
    [Tooltip("Electrodash: how far the atom jumps (world units).")]
    public float dashDistance = 5f;
    public float dashCooldown = 4f;
    [Tooltip("Electrodash: seconds the atom stays untouchable after landing.")]
    public float dashInvulnerable = 0.5f;
    public float blastCooldown = 10f;
    [Tooltip("Electroblast: width of the ray (world units).")]
    public float blastWidth = 2.4f;
    [Tooltip("Electroblast: speed dust and black holes are shoved away at.")]
    public float blastHazardSpeed = 30f;

    public static readonly Color DashColor = new Color(0.55f, 0.85f, 1f);
    public static readonly Color BlastColor = new Color(1f, 0.92f, 0.55f);

    bool abilityAiming;
    int aimStartFrame;
    float abilityCooldown;
    Vector2 aimDir = Vector2.right;
    float aimDistance;
    AbilityAim aimFx;

    public enum AbilityKind { None, Dash, Blast, Throw }

    /// <summary>
    /// The ability the atom has right now: the Excited Atom's (equipped in the Lab), or the Alkali
    /// Metal character's electron throw while its electron count is an alkali metal.
    /// </summary>
    public AbilityKind Ability
    {
        get
        {
            if (character == Character.Excited)
                return Lab.ExcitedAbility == LabPerk.NobleDash ? AbilityKind.Dash
                     : Lab.ExcitedAbility == LabPerk.NobleBlast ? AbilityKind.Blast : AbilityKind.None;
            return IsAlkali ? AbilityKind.Throw : AbilityKind.None;
        }
    }
    public bool AbilityAiming => abilityAiming;
    float AbilityCooldownTime(AbilityKind k) =>
        k == AbilityKind.Dash ? dashCooldown : k == AbilityKind.Blast ? blastCooldown : alkaliCooldown;
    /// <summary>1 right after use (or while the alkali electron is out), 0 when ready again.</summary>
    public float AbilityCooldown01
    {
        get
        {
            AbilityKind k = Ability;
            if (k == AbilityKind.None) return 0f;
            if (k == AbilityKind.Throw && AlkaliShot.Out) return 1f;
            return Mathf.Clamp01(abilityCooldown / Mathf.Max(0.01f, AbilityCooldownTime(k)));
        }
    }
    public bool AbilityHasElectron => pendulum != null && pendulum.BallCount > 0;
    public bool AbilityReady => Ability != AbilityKind.None && abilityCooldown <= 0f && AbilityHasElectron
                                && !(Ability == AbilityKind.Throw && AlkaliShot.Out)
                                && state == State.Playing && !teaching;
    /// <summary>Set by GameUI while the pointer is over the ability button (its own click toggles aiming).</summary>
    public bool PointerOverAbilityButton { get; set; }

    /// <summary>The HUD button: starts aiming, or cancels it if already aiming.</summary>
    public void ToggleAbilityAim()
    {
        if (abilityAiming) { EndAbilityAim(); Sfx.Play("lock", 0.5f); return; }
        if (!AbilityReady) { Sfx.Play("full", 0.5f); return; }
        abilityAiming = true;
        aimStartFrame = Time.frameCount;
        Time.timeScale = 0f;
        if (aimFx == null) aimFx = AbilityAim.Create();
        Sfx.Play("charge", 0.35f);
    }

    /// <param name="resumeTime">False when the caller sets the time scale itself (state changes, new level).</param>
    void EndAbilityAim(bool resumeTime = true)
    {
        abilityAiming = false;
        if (aimFx != null) aimFx.Hide();
        if (resumeTime && state == State.Playing && !teaching) Time.timeScale = 1f;
    }

    /// <summary>While frozen: follow the pointer, then fire on a click or cancel.</summary>
    void UpdateAbilityAim()
    {
        AbilityKind ability = Ability;
        if (ability == AbilityKind.None || pendulum == null || !AbilityHasElectron) { EndAbilityAim(); return; }

        Vector2 origin = pendulum.PivotPosition;
        var pointer = Pointer.current;
        if (pointer != null && cam != null)
        {
            Vector2 sp = pointer.position.ReadValue();
            Vector2 w = cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, -camZ));
            Vector2 d = w - origin;
            if (d.sqrMagnitude > 0.04f) aimDir = d.normalized;
            aimDistance = Mathf.Min(d.magnitude, AlkaliThrowRange);
        }

        Vector2 throwTo = origin + aimDir * aimDistance;
        if (ability == AbilityKind.Dash) aimFx.ShowDash(origin, origin + aimDir * dashDistance, pendulum.BallCount, DashColor);
        else if (ability == AbilityKind.Blast) aimFx.ShowBlast(origin, aimDir, BlastLength, blastWidth, BlastColor);
        else aimFx.ShowThrow(origin, throwTo, AlkaliBlastRadius, AlkaliColor);

        if (Time.frameCount == aimStartFrame) return;   // the click that opened it
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if ((kb != null && kb.escapeKey.wasPressedThisFrame) || (mouse != null && mouse.rightButton.wasPressedThisFrame))
        {
            EndAbilityAim();
            Sfx.Play("lock", 0.5f);
            return;
        }
        bool clicked = pointer != null && pointer.press.wasPressedThisFrame && !PointerOverAbilityButton;
        bool spaced = kb != null && kb.spaceKey.wasPressedThisFrame;   // Space fires at the current aim
        if (clicked || spaced)
        {
            EndAbilityAim();
            if (ability == AbilityKind.Dash) FireDash(origin);
            else if (ability == AbilityKind.Blast) FireBlast(origin);
            else FireThrow(throwTo);
            if (clicked) PendulumController.SuppressInputFrame = Time.frameCount;   // this click doesn't also grab the thruster
        }
    }

    float BlastLength => HalfExtents.magnitude * 2.2f;   // past the edge of the screen from anywhere on it

    /// <summary>Alkali metal: the outermost electron flies to 'to', explodes and comes back.</summary>
    void FireThrow(Vector2 to)
    {
        if (!pendulum.ThrowOutermost(out Vector2 from)) return;
        AlkaliShot.Launch(from, to, this);
        abilityCooldown = alkaliCooldown;
        Sfx.Play("boost", 0.6f);
    }

    void FireDash(Vector2 from)
    {
        if (!pendulum.SacrificeOutermost(DashColor, out _)) return;
        Vector2 to = from + aimDir * dashDistance;
        pendulum.Dash(to - from, dashInvulnerable);
        abilityCooldown = dashCooldown;

        // An overcharged streak along the path, and a flash at both ends.
        for (int i = 0; i <= 14; i++)
        {
            Vector2 p = Vector2.Lerp(from, to, i / 14f);
            for (int k = 0; k < 3; k++)
                ParticleBursts.Emit(p, Random.insideUnitCircle * 3f + aimDir * 2f, Color.Lerp(DashColor, Color.white, Random.value * 0.6f),
                    0.12f, 0.25f + Random.value * 0.25f, 5f);
        }
        ShockRing.Spawn(from, 1.6f, DashColor);
        ShockRing.Spawn(to, 2.4f, Color.white);
        ParticleBursts.Burst(to, DashColor, 24, 8f, 0.25f, 0.5f);
        AbilityAim.Streak(from, to, 0.7f, DashColor, 0.35f);
        Sfx.Play("boost", 0.9f);
        Sfx.Play("zap", 0.5f);
        AddShake(0.25f);
    }

    void FireBlast(Vector2 origin)
    {
        if (!pendulum.SacrificeOutermost(BlastColor, out _)) return;
        abilityCooldown = blastCooldown;
        float length = BlastLength, half = blastWidth * 0.5f;
        Vector2 dir = aimDir;

        // Every enemy in the ray is vaporized.
        for (int i = EnemyMover.All.Count - 1; i >= 0; i--)
        {
            var e = EnemyMover.All[i];
            if (e == null || e.IsDead) continue;
            if (InRay(e.Position, e.radius, origin, dir, length, half)) e.Vaporize(BlastColor);
        }
        // Dust and black holes are shoved out of the way, along the ray (off the screen).
        foreach (var h in Hazard.All)
        {
            if (h == null) continue;
            float r = h.Type == HazardType.Asteroid ? h.radius : h.radius + 0.6f;
            if (!InRay(h.transform.position, r, origin, dir, length, half)) continue;
            h.velocity = dir * blastHazardSpeed + h.velocity * 0.2f;
            ParticleBursts.Burst(h.transform.position, BlastColor, 18, 7f, 0.3f, 0.5f);
        }

        AbilityAim.Streak(origin, origin + dir * length, blastWidth, BlastColor, 0.55f);
        ShockRing.Spawn(origin, 2.6f, BlastColor);
        for (int i = 0; i < 40; i++)
        {
            float t = Random.value * length;
            Vector2 side = new Vector2(-dir.y, dir.x) * Random.Range(-half, half);
            ParticleBursts.Emit(origin + dir * t + side, dir * Random.Range(4f, 10f) + Random.insideUnitCircle * 2f,
                Color.Lerp(BlastColor, Color.white, Random.value * 0.5f), 0.14f, 0.3f + Random.value * 0.3f, 3f);
        }
        Sfx.Play("explode", 0.7f);
        Sfx.Play("zap", 0.8f);
        AddShake(0.6f);
    }

    static bool InRay(Vector2 p, float r, Vector2 origin, Vector2 dir, float length, float half)
    {
        Vector2 rel = p - origin;
        float along = Vector2.Dot(rel, dir);
        if (along < -r || along > length + r) return false;
        float across = Mathf.Abs(rel.x * dir.y - rel.y * dir.x);
        return across <= half + r;
    }
}

/// <summary>World-space previews for the Excited Atom's abilities, and their afterglow streaks.</summary>
public class AbilityAim : MonoBehaviour
{
    LineRenderer path, edgeA, edgeB;
    SpriteRenderer fill, ghost, ghostRing;
    static Material material;

    static Material Mat => material != null ? material : (material = new Material(Shader.Find("Sprites/Default")));

    public static AbilityAim Create()
    {
        var go = new GameObject("AbilityAim");
        var a = go.AddComponent<AbilityAim>();
        a.path = a.Line("Path", 0.06f, 40);
        a.edgeA = a.Line("EdgeA", 0.05f, 40);
        a.edgeB = a.Line("EdgeB", 0.05f, 40);
        a.fill = a.Sprite("Fill", ProcSprites.Pixel, 38);
        a.ghost = a.Sprite("Ghost", ProcSprites.Glow, 39);
        a.ghostRing = a.Sprite("GhostRing", ProcSprites.Ring, 39);
        a.Hide();
        return a;
    }

    LineRenderer Line(string name, float width, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = Mat;
        lr.positionCount = 2;
        lr.widthMultiplier = width;
        lr.numCapVertices = 3;
        lr.sortingOrder = order;
        return lr;
    }

    SpriteRenderer Sprite(string name, Sprite s, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.sortingOrder = order;
        return sr;
    }

    public void Hide() => gameObject.SetActive(false);

    static void SetLine(LineRenderer lr, Vector2 a, Vector2 b, Color c)
    {
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
        lr.startColor = c;
        lr.endColor = new Color(c.r, c.g, c.b, c.a * 0.35f);
    }

    /// <summary>Dash: a pulsing line to the landing spot, with a ghost of the atom there.</summary>
    public void ShowDash(Vector2 from, Vector2 to, int electrons, Color c)
    {
        gameObject.SetActive(true);
        float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 8f);
        SetLine(path, from, to, new Color(c.r, c.g, c.b, 0.8f * pulse));
        edgeA.enabled = edgeB.enabled = fill.enabled = false;
        ghost.enabled = ghostRing.enabled = true;
        ghost.transform.position = to;
        ghost.transform.localScale = Vector3.one * 1.6f;
        ghost.color = new Color(c.r, c.g, c.b, 0.35f * pulse);
        ghostRing.transform.position = to;
        ghostRing.transform.localScale = Vector3.one * (1.2f + 0.15f * pulse);
        ghostRing.color = new Color(1f, 1f, 1f, 0.6f * pulse);
    }

    /// <summary>Alkali throw: a line to the aimed point and the blast circle there.</summary>
    public void ShowThrow(Vector2 from, Vector2 to, float blastRadius, Color c)
    {
        gameObject.SetActive(true);
        float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 8f);
        SetLine(path, from, to, new Color(c.r, c.g, c.b, 0.8f * pulse));
        edgeA.enabled = edgeB.enabled = fill.enabled = false;
        ghost.enabled = ghostRing.enabled = true;
        ghost.transform.position = to;
        ghost.transform.localScale = Vector3.one * blastRadius * 1.4f;
        ghost.color = new Color(c.r, c.g, c.b, 0.18f * pulse);
        ghostRing.transform.position = to;
        ghostRing.transform.localScale = Vector3.one * blastRadius / 0.485f;
        ghostRing.color = new Color(c.r, c.g, c.b, 0.7f * pulse);
    }

    /// <summary>Blast: the band the ray will sweep, to the edge of the screen.</summary>
    public void ShowBlast(Vector2 origin, Vector2 dir, float length, float width, Color c)
    {
        gameObject.SetActive(true);
        float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 8f);
        Vector2 side = new Vector2(-dir.y, dir.x) * width * 0.5f;
        Vector2 end = origin + dir * length;
        edgeA.enabled = edgeB.enabled = fill.enabled = true;
        ghost.enabled = ghostRing.enabled = false;
        SetLine(edgeA, origin + side, end + side, new Color(c.r, c.g, c.b, 0.75f * pulse));
        SetLine(edgeB, origin - side, end - side, new Color(c.r, c.g, c.b, 0.75f * pulse));
        SetLine(path, origin, end, new Color(1f, 1f, 1f, 0.35f * pulse));
        fill.transform.position = origin + dir * length * 0.5f;
        fill.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        fill.transform.localScale = new Vector3(length / PixelWorldSize, width / PixelWorldSize, 1f);
        fill.color = new Color(c.r, c.g, c.b, 0.14f + 0.06f * pulse);
    }

    static float PixelWorldSize => ProcSprites.Pixel != null ? ProcSprites.Pixel.bounds.size.x : 1f;

    /// <summary>A bright band from a to b that fades out over 'life' seconds (the dash trail, the blast ray).</summary>
    public static void Streak(Vector2 a, Vector2 b, float width, Color c, float life)
    {
        var go = new GameObject("AbilityStreak");
        var s = go.AddComponent<AbilityStreak>();
        s.Init(a, b, width, c, life);
    }
}

public class AbilityStreak : MonoBehaviour
{
    SpriteRenderer core, glow;
    Color color;
    float life, maxLife;

    public void Init(Vector2 a, Vector2 b, float w, Color c, float lifetime)
    {
        color = c;
        maxLife = life = Mathf.Max(0.05f, lifetime);
        Vector2 d = b - a;
        transform.position = (a + b) * 0.5f;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        float px = ProcSprites.Pixel != null ? ProcSprites.Pixel.bounds.size.x : 1f;
        glow = Make("Glow", ProcSprites.Pixel, 36, new Vector3(d.magnitude / px, w * 1.6f / px, 1f));
        core = Make("Core", ProcSprites.Pixel, 37, new Vector3(d.magnitude / px, w * 0.55f / px, 1f));
        Apply(1f);
    }

    SpriteRenderer Make(string name, Sprite s, int order, Vector3 scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localScale = scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.sortingOrder = order;
        return sr;
    }

    void Apply(float k)
    {
        glow.color = new Color(color.r, color.g, color.b, 0.35f * k);
        core.color = new Color(1f, 1f, 1f, 0.85f * k * k);
        float s = Mathf.Lerp(0.4f, 1f, k);   // the ray narrows as it fades
        transform.localScale = new Vector3(1f, s, 1f);
    }

    void Update()
    {
        life -= Time.deltaTime;
        if (life <= 0f) { Destroy(gameObject); return; }
        Apply(life / maxLife);
    }
}
