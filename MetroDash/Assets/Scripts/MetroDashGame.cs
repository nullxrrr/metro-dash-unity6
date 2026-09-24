using System;
using System.Collections.Generic;
using UnityEngine;

namespace MetroDash
{
    // A self-contained endless runner. All meshes and sounds are created locally.
    public sealed class MetroDashGame : MonoBehaviour
    {
        enum Mode { Menu, Running, Paused, Finished }
        enum Kind { Train, Hurdle, Gate, Coin, Shield }
        sealed class Item
        {
            public GameObject root;
            public Kind kind;
            public float z;
            public int lane;
        }

        const float LaneWidth = 2.65f;
        const float Gravity = 27f;
        const float JumpVelocity = 11.5f;
        readonly List<Item> items = new List<Item>();
        readonly List<Transform> blocks = new List<Transform>();
        readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        System.Random random = new System.Random();
        Mode mode;
        Transform runner, body, leftLeg, rightLeg, leftArm, rightArm, shieldRing;
        Camera view;
        AudioSource sound;
        AudioClip pickupSound, jumpSound, crashSound, powerSound;
        Material baseMaterial;
        float x, y, verticalSpeed, slideTime, distance, spawnIn, shieldTime, flashTime;
        float animationTime, speed, lastDelta;
        int lane, coins, best;
        bool muted;
        Vector2 touchStart;
        GUIStyle titleStyle, textStyle, smallStyle, bigStyle, buttonStyle;
        Texture2D white;
        bool stylesReady;
        int Score => Mathf.FloorToInt(distance) + coins * 25;

        void Start()
        {
            Application.targetFrameRate = 120;
            best = PlayerPrefs.GetInt("MetroDash.Best", 0);
            muted = PlayerPrefs.GetInt("MetroDash.Muted", 0) != 0;
            baseMaterial = Resources.Load<Material>("RunnerBase");
            BuildWorld();
            BuildRunner();
            sound = gameObject.AddComponent<AudioSource>();
            sound.volume = 0.28f;
            pickupSound = Tone(950, 1450, 0.09f);
            jumpSound = Tone(320, 620, 0.13f);
            crashSound = Tone(150, 45, 0.35f);
            powerSound = Tone(550, 1500, 0.28f);
            ResetRun();
            mode = Mode.Menu;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-runnerSmokeTest") >= 0)
                SmokeTest();
        }

        Material Mat(string name, Color color, bool glow = false)
        {
            if (materials.TryGetValue(name, out Material found)) return found;
            var m = baseMaterial != null ? new Material(baseMaterial) : new Material(Shader.Find("Standard"));
            m.name = name;
            m.color = color;
            m.SetFloat("_Glossiness", 0.22f);
            if (glow) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * 0.7f); }
            materials.Add(name, m);
            return m;
        }

        GameObject Shape(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(go.GetComponent<Collider>());
            return go;
        }
        GameObject Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
            => Shape(name, PrimitiveType.Cube, parent, position, scale, material);

        void BuildWorld()
        {
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.09f, 0.15f, 0.25f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 65;
            RenderSettings.fogEndDistance = 150;
            RenderSettings.ambientLight = new Color(0.53f, 0.60f, 0.75f);
            var sun = new GameObject("Evening sunlight").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.83f, 0.66f);
            sun.intensity = 1.35f;
            sun.transform.rotation = Quaternion.Euler(42, -35, 0);
            sun.shadows = LightShadows.Soft;
            view = new GameObject("Runner Camera").AddComponent<Camera>();
            view.tag = "MainCamera";
            view.clearFlags = CameraClearFlags.SolidColor;
            view.backgroundColor = RenderSettings.fogColor;
            view.fieldOfView = 62;
            view.farClipPlane = 190;
            view.transform.position = new Vector3(0, 5.6f, -9);
            view.transform.rotation = Quaternion.Euler(17, 0, 0);
            view.gameObject.AddComponent<AudioListener>();
            var ground = Mat("Asphalt", new Color(0.12f, 0.18f, 0.23f));
            var rail = Mat("Rails", new Color(0.46f, 0.64f, 0.69f));
            var sleeper = Mat("Sleepers", new Color(0.21f, 0.27f, 0.31f));
            var edge = Mat("Platform", new Color(0.30f, 0.37f, 0.44f));
            var cyan = Mat("Neon mint", new Color(0.15f, 0.95f, 0.79f), true);
            var window = Mat("Window light", new Color(1, 0.69f, 0.30f), true);
            var sceneryRandom = new System.Random(71);
            for (int i = 0; i < 9; i++)
            {
                var chunk = new GameObject("Street block " + i).transform;
                chunk.position = new Vector3(0, 0, i * 22 - 22);
                blocks.Add(chunk);
                Box("Track bed", chunk, new Vector3(0, -0.25f, 11), new Vector3(9, 0.45f, 22), ground);
                for (int l = -1; l <= 1; l++)
                {
                    foreach (float side in new[] { -0.76f, 0.76f })
                        Box("Rail", chunk, new Vector3(l * LaneWidth + side, 0.02f, 11), new Vector3(0.09f, 0.1f, 22), rail);
                    for (int j = 0; j < 11; j++)
                        Box("Sleeper", chunk, new Vector3(l * LaneWidth, -0.005f, j * 2), new Vector3(1.95f, 0.08f, 0.26f), sleeper);
                }
                foreach (int s in new[] { -1, 1 })
                {
                    Box("Platform", chunk, new Vector3(s * 5.1f, 0.04f, 11), new Vector3(1.25f, 0.55f, 22), edge);
                    Box("Platform glow", chunk, new Vector3(s * 4.48f, 0.33f, 11), new Vector3(0.08f, 0.06f, 22), cyan);
                    for (int b = 0; b < 3; b++)
                    {
                        float h = 5 + sceneryRandom.Next(11);
                        var wall = Mat("Facade " + i + " " + s + " " + b,
                            new Color(0.17f + (float)sceneryRandom.NextDouble() * .12f, .24f, .34f + (float)sceneryRandom.NextDouble() * .15f));
                        Box("City building", chunk, new Vector3(s * 9, h / 2, b * 7.3f), new Vector3(5, h, 6.4f), wall);
                        for (int floor = 1; floor < h - 1; floor += 2)
                            Box("Lit window strip", chunk, new Vector3(s * 6.47f, floor, b * 7.3f), new Vector3(.04f, .62f, 3.8f), window);
                    }
                    Box("Lamp post", chunk, new Vector3(s * 5, 2.8f, 10), new Vector3(.13f, 5, .13f), rail);
                    Box("Lamp", chunk, new Vector3(s * 4.6f, 5.25f, 10), new Vector3(1.1f, .15f, .45f), cyan);
                }
            }
        }

        void BuildRunner()
        {
            runner = new GameObject("Runner").transform;
            body = new GameObject("Animated body").transform;
            body.SetParent(runner, false);
            var jacket = Mat("Coral jacket", new Color(1, .27f, .25f));
            var trousers = Mat("Navy trousers", new Color(.08f, .12f, .24f));
            var skin = Mat("Skin", new Color(.80f, .52f, .34f));
            var mint = materials["Neon mint"];
            var shoes = Mat("Sneakers", new Color(.94f, .95f, .88f));
            Box("Jacket", body, new Vector3(0, 1.15f, 0), new Vector3(.65f, .67f, .4f), jacket);
            Shape("Head", PrimitiveType.Sphere, body, new Vector3(0, 1.79f, 0), new Vector3(.49f, .53f, .48f), skin);
            Box("Cap", body, new Vector3(0, 2.02f, 0), new Vector3(.54f, .17f, .54f), mint);
            Box("Cap visor", body, new Vector3(0, 1.99f, .29f), new Vector3(.53f, .06f, .3f), mint);
            Box("Backpack", body, new Vector3(0, 1.18f, -.28f), new Vector3(.42f, .49f, .24f), trousers);
            Box("Backpack stripe", body, new Vector3(0, 1.18f, -.408f), new Vector3(.26f, .07f, .025f), mint);
            leftLeg = Limb("Left leg", new Vector3(-.2f, .83f, 0), new Vector3(.24f, .64f, .26f), trousers);
            rightLeg = Limb("Right leg", new Vector3(.2f, .83f, 0), new Vector3(.24f, .64f, .26f), trousers);
            Box("Shoe", leftLeg, new Vector3(0, -.67f, .08f), new Vector3(.28f, .18f, .46f), shoes);
            Box("Shoe", rightLeg, new Vector3(0, -.67f, .08f), new Vector3(.28f, .18f, .46f), shoes);
            leftArm = Limb("Left arm", new Vector3(-.43f, 1.43f, 0), new Vector3(.2f, .57f, .23f), jacket);
            rightArm = Limb("Right arm", new Vector3(.43f, 1.43f, 0), new Vector3(.2f, .57f, .23f), jacket);
            shieldRing = new GameObject("Shield aura").transform;
            shieldRing.SetParent(runner, false);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6;
                Shape("Shield spark", PrimitiveType.Sphere, shieldRing, new Vector3(Mathf.Cos(a) * .85f, 1, Mathf.Sin(a) * .85f), Vector3.one * .12f, mint);
            }
            shieldRing.gameObject.SetActive(false);
        }

        Transform Limb(string name, Vector3 position, Vector3 size, Material material)
        {
            var pivot = new GameObject(name).transform;
            pivot.SetParent(body, false);
            pivot.localPosition = position;
            Box(name + " mesh", pivot, new Vector3(0, -size.y / 2, 0), size, material);
            return pivot;
        }

        void ResetRun()
        {
            foreach (var item in items) Destroy(item.root);
            items.Clear();
            lane = coins = 0;
            x = y = verticalSpeed = distance = slideTime = shieldTime = flashTime = animationTime = 0;
            speed = 13;
            spawnIn = 45;
            for (int i = 0; i < blocks.Count; i++) blocks[i].position = new Vector3(0, 0, i * 22 - 22);
            for (int i = 0; i < 8; i++) Spawn(Kind.Coin, 0, 14 + i * 2.4f);
            runner.position = Vector3.zero;
            mode = Mode.Running;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.M)) ToggleMute();
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
            {
                if (mode == Mode.Running) mode = Mode.Paused;
                else if (mode == Mode.Paused) mode = Mode.Running;
            }
            if ((mode == Mode.Menu || mode == Mode.Finished) && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space)))
            { ResetRun(); return; }
            if (mode == Mode.Finished && Input.GetKeyDown(KeyCode.R)) { ResetRun(); return; }
            float dt = Mathf.Min(Time.deltaTime, .05f);
            if (mode == Mode.Running)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) MoveLane(-1);
                if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) MoveLane(1);
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.Space)) Jump();
                if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Slide();
                HandleSwipe();
                Tick(dt);
            }
            if (mode != Mode.Paused)
            {
                animationTime += dt * (mode == Mode.Running ? speed : 3);
                AnimateRunner();
            }
            view.transform.position = Vector3.Lerp(view.transform.position, new Vector3(x * .28f, 5.6f + y * .12f, -9), dt * 6);
            view.fieldOfView = Mathf.Lerp(view.fieldOfView, 62 + (speed - 13) * .6f, dt * 2);
        }

        void MoveLane(int direction) { lane = Mathf.Clamp(lane + direction, -1, 1); }
        void Jump()
        {
            if (y > .01f) return;
            slideTime = 0;
            verticalSpeed = JumpVelocity;
            Play(jumpSound);
        }
        void Slide()
        {
            slideTime = .85f;
            if (y > 0) verticalSpeed = -18;
        }
        void HandleSwipe()
        {
            if (Input.touchCount == 0) return;
            Touch t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began) touchStart = t.position;
            if (t.phase != TouchPhase.Ended) return;
            Vector2 delta = t.position - touchStart;
            if (delta.magnitude < 35) return;
            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y)) MoveLane(delta.x > 0 ? 1 : -1);
            else if (delta.y > 0) Jump(); else Slide();
        }

        void Tick(float dt)
        {
            speed = Mathf.Min(27, 13 + distance / 240);
            lastDelta = speed * dt;
            distance += lastDelta;
            x = Mathf.MoveTowards(x, lane * LaneWidth, dt * 17);
            verticalSpeed -= Gravity * dt;
            y = Mathf.Max(0, y + verticalSpeed * dt);
            if (y <= 0) verticalSpeed = Mathf.Max(0, verticalSpeed);
            slideTime = Mathf.Max(0, slideTime - dt);
            shieldTime = Mathf.Max(0, shieldTime - dt);
            flashTime = Mathf.Max(0, flashTime - dt);
            foreach (Transform block in blocks)
            {
                block.position -= Vector3.forward * lastDelta;
                if (block.position.z < -44) block.position += Vector3.forward * (blocks.Count * 22);
            }
            spawnIn -= lastDelta;
            if (spawnIn <= 0) { SpawnRow(115); spawnIn += 22 + speed * .4f; }
            for (int i = items.Count - 1; i >= 0; i--)
            {
                Item item = items[i];
                float previousZ = item.z;
                item.z -= lastDelta;
                item.root.transform.position = new Vector3(item.lane * LaneWidth, 0, item.z);
                if (item.kind == Kind.Coin || item.kind == Kind.Shield)
                    item.root.transform.GetChild(0).Rotate(0, 160 * dt, 0, Space.World);
                bool nearLane = Mathf.Abs(x - item.lane * LaneWidth) < (item.kind == Kind.Coin ? .95f : 1.04f);
                float reach = item.kind == Kind.Train ? 4.8f : .85f;
                bool nearZ = item.z < reach && previousZ > -reach;
                if (nearLane && nearZ)
                {
                    if (item.kind == Kind.Coin && y < 1.65f)
                    { coins++; Play(pickupSound); Remove(i); continue; }
                    if (item.kind == Kind.Shield && y < 1.65f)
                    { shieldTime = 9; Play(powerSound); Remove(i); continue; }
                    bool collision = item.kind == Kind.Train
                        || (item.kind == Kind.Hurdle && y < 1.05f)
                        || (item.kind == Kind.Gate && !(slideTime > 0 && y < .12f));
                    if (collision)
                    {
                        if (shieldTime > 0) { shieldTime = 0; flashTime = .35f; Play(powerSound); Remove(i); continue; }
                        Finish();
                        break;
                    }
                }
                if (item.z < -16) Remove(i);
            }
        }

        void Remove(int index) { Destroy(items[index].root); items.RemoveAt(index); }

        void SpawnRow(float z)
        {
            // Every row has a completely open lane; spacing allows a full two-lane crossing.
            int safeLane = random.Next(-1, 2);
            for (int l = -1; l <= 1; l++)
                if (l != safeLane && random.NextDouble() < .88)
                    Spawn((Kind)random.Next(0, 3), l, z);
            for (int c = 0; c < 6; c++) Spawn(Kind.Coin, safeLane, z - 6 + c * 2.2f);
            if (random.NextDouble() < .18) Spawn(Kind.Shield, safeLane, z + 9);
        }

        void Spawn(Kind kind, int atLane, float z)
        {
            var root = new GameObject(kind.ToString());
            root.transform.position = new Vector3(atLane * LaneWidth, 0, z);
            var t = root.transform;
            var orange = Mat("Warning amber", new Color(1, .62f, .16f));
            var dark = Mat("Train glass", new Color(.04f, .14f, .22f));
            var mint = materials["Neon mint"];
            if (kind == Kind.Train)
            {
                var paint = Mat("Train blue", new Color(.13f, .49f, .73f));
                Box("Train body", t, new Vector3(0, 1.65f, 0), new Vector3(2.05f, 2.8f, 8.5f), paint);
                Box("Roof", t, new Vector3(0, 3.1f, 0), new Vector3(2.12f, .18f, 8.6f), mint);
                Box("Windshield", t, new Vector3(0, 2.1f, -4.27f), new Vector3(1.7f, .95f, .05f), dark);
                Box("Front stripe", t, new Vector3(0, 1.2f, -4.29f), new Vector3(2.06f, .23f, .04f), orange);
                foreach (int s in new[] { -1, 1 })
                {
                    Box("Headlight", t, new Vector3(s * .7f, .74f, -4.3f), new Vector3(.28f, .25f, .06f), materials["Window light"]);
                    for (int w = 0; w < 5; w++)
                        Box("Side window", t, new Vector3(s * 1.035f, 2.15f, -3 + w * 1.5f), new Vector3(.035f, .85f, 1.04f), dark);
                }
            }
            else if (kind == Kind.Hurdle)
            {
                Box("Jump barrier", t, new Vector3(0, .6f, 0), new Vector3(2.05f, 1.2f, .6f), orange);
                for (int s = -1; s <= 1; s++)
                    Box("Barrier stripe", t, new Vector3(s * .65f, .6f, -.315f), new Vector3(.28f, 1.1f, .035f), dark);
            }
            else if (kind == Kind.Gate)
            {
                foreach (int s in new[] { -1, 1 })
                    Box("Gate post", t, new Vector3(s * 1.1f, 1.3f, 0), new Vector3(.17f, 2.6f, .4f), orange);
                Box("Slide under beam", t, new Vector3(0, 2.03f, 0), new Vector3(2.35f, 1.45f, .6f), orange);
                Box("Slide indicator", t, new Vector3(0, 2, -.32f), new Vector3(1.55f, .18f, .04f), dark);
            }
            else if (kind == Kind.Coin)
            {
                var gold = Mat("Gold", new Color(1, .76f, .15f), true);
                var coin = Shape("Coin", PrimitiveType.Cylinder, t, new Vector3(0, 1.1f, 0), new Vector3(.54f, .085f, .54f), gold);
                coin.transform.localRotation = Quaternion.Euler(90, 0, 0);
            }
            else
            {
                var shield = Shape("Shield pickup", PrimitiveType.Cube, t, new Vector3(0, 1.2f, 0), Vector3.one * .65f, mint);
                shield.transform.localRotation = Quaternion.Euler(35, 0, 45);
            }
            items.Add(new Item { root = root, kind = kind, lane = atLane, z = z });
        }

        void AnimateRunner()
        {
            runner.position = new Vector3(x, y, 0);
            bool sliding = slideTime > 0;
            body.localScale = sliding ? new Vector3(1, .43f, 1.25f) : Vector3.one;
            body.localRotation = Quaternion.Euler(sliding ? -12 : 0, 0, (x - lane * LaneWidth) * 7);
            float swing = mode == Mode.Running && y <= 0 && !sliding ? Mathf.Sin(animationTime * 1.2f) * 37 : 0;
            leftLeg.localRotation = Quaternion.Euler(swing, 0, 0);
            rightLeg.localRotation = Quaternion.Euler(-swing, 0, 0);
            leftArm.localRotation = Quaternion.Euler(-swing * .8f - 12, 0, -10);
            rightArm.localRotation = Quaternion.Euler(swing * .8f - 12, 0, 10);
            shieldRing.gameObject.SetActive(shieldTime > 0);
            shieldRing.Rotate(0, Time.deltaTime * 150, 0);
        }
        void Finish()
        {
            mode = Mode.Finished;
            flashTime = .4f;
            Play(crashSound);
            if (Score > best) { best = Score; PlayerPrefs.SetInt("MetroDash.Best", best); PlayerPrefs.Save(); }
        }
        void OnApplicationFocus(bool focus) { if (!focus && mode == Mode.Running) mode = Mode.Paused; }
        void ToggleMute()
        {
            muted = !muted;
            PlayerPrefs.SetInt("MetroDash.Muted", muted ? 1 : 0);
            PlayerPrefs.Save();
        }
        void Play(AudioClip clip) { if (!muted && sound != null) sound.PlayOneShot(clip); }
        AudioClip Tone(float start, float end, float duration)
        {
            int count = Mathf.CeilToInt(44100 * duration);
            float[] samples = new float[count];
            float phase = 0;
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / count;
                phase += Mathf.Lerp(start, end, t) * 2 * Mathf.PI / 44100;
                samples[i] = Mathf.Sin(phase) * Mathf.Sin(Mathf.PI * t) * (1 - t) * .55f;
            }
            AudioClip clip = AudioClip.Create("Synth tone", count, 1, 44100, false);
            clip.SetData(samples, 0);
            return clip;
        }

        void InitStyles()
        {
            if (stylesReady) return;
            stylesReady = true;
            white = Texture2D.whiteTexture;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 56, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            titleStyle.normal.textColor = Color.white;
            textStyle = new GUIStyle(titleStyle) { fontSize = 22, fontStyle = FontStyle.Normal };
            smallStyle = new GUIStyle(textStyle) { fontSize = 16 };
            bigStyle = new GUIStyle(titleStyle) { fontSize = 34, alignment = TextAnchor.MiddleLeft };
            buttonStyle = new GUIStyle(textStyle) { fontStyle = FontStyle.Bold };
        }
        void Panel(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, white);
            GUI.color = Color.white;
        }
        void Label(Rect rect, string text, GUIStyle style, Color color)
        {
            Color previous = style.normal.textColor;
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
            style.normal.textColor = previous;
        }
        bool Button(Rect rect, string text, bool primary = false)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            Panel(rect, primary ? (hover ? new Color(.4f, 1, .85f) : new Color(.15f, .91f, .74f)) : new Color(.19f, .26f, .36f, hover ? 1 : .85f));
            Label(rect, text, buttonStyle, primary ? new Color(.04f, .13f, .19f) : Color.white);
            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }

        void OnGUI()
        {
            InitStyles();
            float scale = Mathf.Min(Screen.width / 1280f, Screen.height / 720f);
            float offsetX = (Screen.width - 1280 * scale) / 2;
            float offsetY = (Screen.height - 720 * scale) / 2;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offsetX, offsetY, 0), Quaternion.identity, Vector3.one * scale);
            Color mint = new Color(.26f, 1, .81f);
            Color secondary = new Color(.65f, .74f, .85f);
            Panel(new Rect(30, 26, 240, 93), new Color(.04f, .08f, .14f, .87f));
            Label(new Rect(48, 34, 200, 25), "METRO DASH / SCORE", smallStyle, mint);
            Label(new Rect(52, 60, 200, 52), Score.ToString("000000"), bigStyle, Color.white);
            Panel(new Rect(1010, 26, 240, 93), new Color(.04f, .08f, .14f, .87f));
            Label(new Rect(1028, 32, 205, 35), "COINS   " + coins.ToString("000"), textStyle, new Color(1, .79f, .28f));
            Label(new Rect(1028, 75, 205, 28), "BEST  " + best.ToString("000000"), smallStyle, secondary);
            if (mode == Mode.Running)
            {
                Label(new Rect(400, 30, 480, 35), Mathf.FloorToInt(distance) + " m    /    " + speed.ToString("0.0") + " m/s", textStyle, Color.white);
                if (shieldTime > 0)
                {
                    Panel(new Rect(480, 80, 320, 6), new Color(.12f, .25f, .31f));
                    Panel(new Rect(480, 80, 320 * shieldTime / 9, 6), mint);
                    Label(new Rect(470, 91, 340, 28), "SHIELD  /  " + shieldTime.ToString("0.0") + "s", smallStyle, mint);
                }
                Label(new Rect(160, 666, 960, 26), "A / D  MOVE     SPACE  JUMP     S  SLIDE     P  PAUSE     M  SOUND", smallStyle, Color.white);
                if (Button(new Rect(1145, 637, 105, 43), "PAUSE")) mode = Mode.Paused;
            }
            else
            {
                Panel(new Rect(-offsetX / scale, -offsetY / scale, Screen.width / scale, Screen.height / scale), new Color(.025f, .055f, .10f, .75f));
                Panel(new Rect(305, 136, 670, 472), new Color(.055f, .10f, .17f, .97f));
                Panel(new Rect(305, 136, 670, 5), mint);
                Label(new Rect(330, 163, 620, 32), "THE CITY IS YOUR RUNWAY", smallStyle, mint);
                string title = mode == Mode.Menu ? "METRO DASH" : mode == Mode.Paused ? "TAKE A BREATH" : "END OF THE LINE";
                Label(new Rect(325, 199, 630, 85), title, titleStyle, Color.white);
                string subtitle = mode == Mode.Menu ? "Three lanes. One more run." : mode == Mode.Paused ? "Your next move is waiting." : "SCORE " + Score + "     COINS " + coins + "     BEST " + best;
                Label(new Rect(325, 291, 630, 40), subtitle, textStyle, secondary);
                Label(new Rect(345, 344, 590, 30), "A / D or arrows: change lanes   |   SPACE: jump", smallStyle, Color.white);
                Label(new Rect(345, 378, 590, 30), "S / Down: slide   |   Mint pickup: one-hit shield", smallStyle, Color.white);
                if (Button(new Rect(400, 438, 480, 62), mode == Mode.Menu ? "LET'S RUN   >" : mode == Mode.Paused ? "KEEP RUNNING   >" : "RUN AGAIN   >", true))
                { if (mode == Mode.Paused) mode = Mode.Running; else ResetRun(); }
                if (Button(new Rect(400, 520, 230, 44), muted ? "SOUND: OFF" : "SOUND: ON")) ToggleMute();
                if (Button(new Rect(650, 520, 230, 44), "QUIT"))
                {
                    Application.Quit();
                    #if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
                    #endif
                }
                Label(new Rect(350, 630, 580, 30), "An original endless runner  /  Made with Unity", smallStyle, secondary);
            }
            if (flashTime > 0 && mode == Mode.Running)
                Panel(new Rect(0, 0, 1280, 720), new Color(.2f, 1, .8f, flashTime * .55f));
            GUI.matrix = Matrix4x4.identity;
        }

        void SmokeTest()
        {
            try
            {
                muted = true;
                ResetRun();
                MoveLane(-1); MoveLane(-1);
                Require(lane == -1, "lane bounds");
                for (int i = 0; i < 30; i++) Tick(1f / 60);
                Require(Mathf.Abs(x + LaneWidth) < .01f, "lane movement");
                Jump();
                for (int i = 0; i < 20; i++) Tick(1f / 60);
                Require(y > 1.1f, "jump clears hurdle");
                for (int i = 0; i < 60; i++) Tick(1f / 60);
                Require(y == 0, "landing");
                ResetRun();
                Spawn(Kind.Coin, 0, .1f); Tick(.016f);
                Require(coins == 1, "coin collection");
                Spawn(Kind.Hurdle, 0, .1f); y = 1.5f; Tick(.016f);
                Require(mode == Mode.Running, "hurdle jump");
                ResetRun(); Slide(); Spawn(Kind.Gate, 0, .1f); Tick(.016f);
                Require(mode == Mode.Running, "gate slide");
                ResetRun(); Spawn(Kind.Shield, 0, .1f); Tick(.016f);
                Require(shieldTime > 8, "shield pickup");
                Spawn(Kind.Train, 0, .1f); Tick(.016f);
                Require(mode == Mode.Running && shieldTime == 0, "shield absorbs train");
                Spawn(Kind.Train, 0, .1f); Tick(.016f);
                Require(mode == Mode.Finished, "train collision");
                ResetRun(); Spawn(Kind.Hurdle, 0, .1f); Tick(.016f);
                Require(mode == Mode.Finished, "hurdle collision");
                ResetRun(); Spawn(Kind.Gate, 0, .1f); Tick(.016f);
                Require(mode == Mode.Finished, "gate collision");
                ResetRun();
                Require(coins == 0 && distance == 0 && mode == Mode.Running, "restart");
                for (int n = 0; n < 100; n++)
                {
                    ResetRun(); SpawnRow(100);
                    var blocked = new HashSet<int>();
                    foreach (var item in items) if (item.kind <= Kind.Gate) blocked.Add(item.lane);
                    Require(blocked.Count <= 2, "reachable open lane");
                }
                Debug.Log("METRO_DASH_SMOKE_TEST: PASS (movement, jump, landing, coins, slide, shield, collisions, restart, 100 safe rows)");
                Application.Quit(0);
            }
            catch (Exception e) { Debug.LogException(e); Application.Quit(1); }
        }
        static void Require(bool condition, string description)
        { if (!condition) throw new Exception("Runner test failed: " + description); }
    }
}
