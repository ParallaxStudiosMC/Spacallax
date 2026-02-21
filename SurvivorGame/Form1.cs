using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using p2dgfx;

namespace SurvivorGame
{
    static class Program
    {
        // Game states
        enum GameState { Menu, Playing, GameOver, Settings }
        static GameState currentState = GameState.Menu;

        // Difficulty (added Seriously Insane)
        enum Difficulty { Easy, Medium, Hard, Insane, SeriouslyInsane, Unbeatable, BlindNightmare }
        static Difficulty currentDifficulty = Difficulty.Medium;
        static string[] difficultyNames = {
            "Easy", "Medium", "Hard",
            "INSANE",
            "SERIOUSLY INSANE",
            "UNBEATABLE!",
            "BLIND NIGHTMARE"
        };

        // Settings class
        class Settings
        {
            public int Width { get; set; } = 800;
            public int Height { get; set; } = 600;
            public bool Fullscreen { get; set; } = false;
            public float MasterVolume { get; set; } = 0.8f;
            public float SoundVolume { get; set; } = 0.8f;
            public float MusicVolume { get; set; } = 0.8f;
            public bool VSync { get; set; } = true;
        }
        static Settings gameSettings;
        const string settingsFile = "spacallax_settings.json";

        // Game objects
        static Player player;
        static List<Enemy> enemies = new List<Enemy>();
        static List<Bullet> playerBullets = new List<Bullet>();
        static List<Bullet> enemyBullets = new List<Bullet>();
        static List<PowerUp> powerUps = new List<PowerUp>();
        static List<Particle> particles = new List<Particle>();
        static List<ScorePopup> scorePopups = new List<ScorePopup>();

        // Sprite IDs
        static int playerSpriteId = -1;
        static int enemySpriteId = -1;
        static int bulletSpriteId = -1;

        // Game state
        static float enemySpawnTimer = 0f;
        static float enemySpawnInterval = 0.4f;
        static float powerUpSpawnTimer = 0f;
        static float powerUpSpawnInterval = 5f;
        static int score = 0;
        static int highScore = 0;

        // Difficulty scaling (wave counter removed)
        static float enemyBaseSpeed = 200f;
        static float enemySpeedMultiplier = 1f;
        static float spawnIntervalMultiplier = 1f;

        // Special mode flags
        static bool insaneMode = false;
        static bool seriouslyInsaneMode = false;   // new
        static bool unbeatableMode = false;
        static bool blindNightmare = false;

        // Cooldowns
        static float shotCooldownTimer = 0f;
        const float shotCooldownDuration = 5f;
        const float blindShotCooldown = 2f;

        // Power-up timers
        static float rapidFireTimer = 0f;
        static float shieldTimer = 0f;
        static float scoreMultiplierTimer = 0f;
        const float rapidFireDuration = 5f;
        const float shieldDuration = 5f;
        const float scoreMultiplierDuration = 5f;

        // Player customization (fallback)
        static Color playerColor = Color.LimeGreen;

        // Starfield
        static List<Star> stars = new List<Star>();

        // Object pooling
        static Stack<Bullet> bulletPool = new Stack<Bullet>();
        static Stack<Bullet> enemyBulletPool = new Stack<Bullet>();

        // Menu selection
        static int selectedDifficultyIndex = 1;

        // Settings menu variables
        static int selectedSettingIndex = 0;
        static string[] settingNames = {
            "Resolution",
            "Fullscreen",
            "VSync",
            "Master Volume",
            "Sound Volume",
            "Music Volume",
            "Back"
        };
        static string[] resolutionOptions = {
            "800x600", "1024x768", "1280x720", "1920x1080",
            "3840x2160", "7680x4320"
        };
        static int selectedResolutionIndex = 0;
        static bool settingsChanged = false;

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            LoadSettings();

            // Apply volume settings at start
            Engine.SetMasterVolume(gameSettings.MasterVolume);
            Engine.SetSoundVolume(gameSettings.SoundVolume);
            Engine.SetMusicVolume(gameSettings.MusicVolume);

            string currentRes = $"{gameSettings.Width}x{gameSettings.Height}";
            selectedResolutionIndex = Array.IndexOf(resolutionOptions, currentRes);
            if (selectedResolutionIndex < 0) selectedResolutionIndex = 0;

            playerSpriteId = Engine.LoadSprite("spaceship_player.png");
            enemySpriteId = Engine.LoadSprite("spaceship_enemy.png");
            bulletSpriteId = Engine.LoadSprite("sprite_bullet.png");

            player = new Player { X = gameSettings.Width / 2, Y = gameSettings.Height - 100, Size = 40 };

            RegenerateStars();

            Engine.SetTargetFrameRate(120);
            Engine.Init(gameSettings.Width, gameSettings.Height, "SPACALLAX 1.0.0", Update, Draw);
        }

        static void LoadSettings()
        {
            if (File.Exists(settingsFile))
            {
                try
                {
                    string json = File.ReadAllText(settingsFile);
                    gameSettings = JsonSerializer.Deserialize<Settings>(json);
                }
                catch
                {
                    gameSettings = new Settings();
                }
            }
            else
            {
                gameSettings = new Settings();
            }
        }

        static void SaveSettings()
        {
            try
            {
                string json = JsonSerializer.Serialize(gameSettings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(settingsFile, json);
            }
            catch { }
        }

        static void RegenerateStars()
        {
            stars.Clear();
            int starCount = 200;
            for (int i = 0; i < starCount; i++)
            {
                stars.Add(new Star
                {
                    X = Engine.RandomFloat(0, Engine.WindowWidth),
                    Y = Engine.RandomFloat(0, Engine.WindowHeight),
                    Speed = Engine.RandomFloat(20, 100),
                    Brightness = Engine.RandomFloat(0.3f, 1f),
                    Size = Engine.RandomInt(1, 3)
                });
            }
        }

        static void Update(float dt)
        {
            if (Input.GetKeyDown(Keys.F11))
            {
                Engine.ToggleFullscreen();
                gameSettings.Fullscreen = Engine.Fullscreen;
                SaveSettings();
                RegenerateStars();
            }

            switch (currentState)
            {
                case GameState.Menu:
                    UpdateMenu(dt);
                    break;
                case GameState.Playing:
                    UpdatePlaying(dt);
                    break;
                case GameState.GameOver:
                    UpdateGameOver(dt);
                    break;
                case GameState.Settings:
                    UpdateSettings(dt);
                    break;
            }

            UpdateStars(dt);
        }

        static void UpdateMenu(float dt)
        {
            if (Input.GetKeyDown(Keys.Left) || Input.GetKeyDown(Keys.A))
            {
                selectedDifficultyIndex--;
                if (selectedDifficultyIndex < 0) selectedDifficultyIndex = 6; // now 7 options (0-6)
            }
            if (Input.GetKeyDown(Keys.Right) || Input.GetKeyDown(Keys.D))
            {
                selectedDifficultyIndex++;
                if (selectedDifficultyIndex > 6) selectedDifficultyIndex = 0;
            }

            if (Input.GetKeyDown(Keys.Enter) || Input.GetKeyDown(Keys.Space))
            {
                currentDifficulty = (Difficulty)selectedDifficultyIndex;
                insaneMode = (currentDifficulty == Difficulty.Insane);
                seriouslyInsaneMode = (currentDifficulty == Difficulty.SeriouslyInsane);
                unbeatableMode = (currentDifficulty == Difficulty.Unbeatable);
                blindNightmare = (currentDifficulty == Difficulty.BlindNightmare);
                StartGame();
            }

            if (Input.GetKeyDown(Keys.S))
            {
                currentState = GameState.Settings;
                selectedSettingIndex = 0;
            }
        }

        static void UpdateSettings(float dt)
        {
            if (Input.GetKeyDown(Keys.Up) || Input.GetKeyDown(Keys.W))
            {
                selectedSettingIndex--;
                if (selectedSettingIndex < 0) selectedSettingIndex = settingNames.Length - 1;
            }
            if (Input.GetKeyDown(Keys.Down) || Input.GetKeyDown(Keys.S))
            {
                selectedSettingIndex++;
                if (selectedSettingIndex >= settingNames.Length) selectedSettingIndex = 0;
            }

            if (Input.GetKeyDown(Keys.Left) || Input.GetKeyDown(Keys.A))
            {
                AdjustSetting(-1);
            }
            if (Input.GetKeyDown(Keys.Right) || Input.GetKeyDown(Keys.D))
            {
                AdjustSetting(1);
            }

            if (Input.GetKeyDown(Keys.Escape) || (selectedSettingIndex == settingNames.Length - 1 && Input.GetKeyDown(Keys.Enter)))
            {
                SaveSettings();
                currentState = GameState.Menu;
            }
        }

        static void AdjustSetting(int delta)
        {
            switch (selectedSettingIndex)
            {
                case 0:
                    selectedResolutionIndex += delta;
                    if (selectedResolutionIndex < 0) selectedResolutionIndex = resolutionOptions.Length - 1;
                    if (selectedResolutionIndex >= resolutionOptions.Length) selectedResolutionIndex = 0;
                    string[] parts = resolutionOptions[selectedResolutionIndex].Split('x');
                    gameSettings.Width = int.Parse(parts[0]);
                    gameSettings.Height = int.Parse(parts[1]);
                    Engine.SetWindowSize(gameSettings.Width, gameSettings.Height);
                    RegenerateStars();
                    break;
                case 1:
                    // Fullscreen toggled with F11 only
                    break;
                case 2:
                    gameSettings.VSync = !gameSettings.VSync;
                    Engine.SetVSync(gameSettings.VSync);
                    break;
                case 3:
                    gameSettings.MasterVolume = Math.Clamp(gameSettings.MasterVolume + delta * 0.05f, 0f, 1f);
                    Engine.SetMasterVolume(gameSettings.MasterVolume);
                    break;
                case 4:
                    gameSettings.SoundVolume = Math.Clamp(gameSettings.SoundVolume + delta * 0.05f, 0f, 1f);
                    Engine.SetSoundVolume(gameSettings.SoundVolume);
                    break;
                case 5:
                    gameSettings.MusicVolume = Math.Clamp(gameSettings.MusicVolume + delta * 0.05f, 0f, 1f);
                    Engine.SetMusicVolume(gameSettings.MusicVolume);
                    break;
            }
        }

        static void StartGame()
        {
            // Base settings per difficulty (including Seriously Insane)
            switch (currentDifficulty)
            {
                case Difficulty.Easy:
                    enemyBaseSpeed = 150f;
                    enemySpawnInterval = 0.6f;
                    player.BulletSpeed = 700f;
                    break;
                case Difficulty.Medium:
                    enemyBaseSpeed = 200f;
                    enemySpawnInterval = 0.4f;
                    player.BulletSpeed = 600f;
                    break;
                case Difficulty.Hard:
                    enemyBaseSpeed = 300f;
                    enemySpawnInterval = 0.25f;
                    player.BulletSpeed = 500f;
                    break;
                case Difficulty.Insane:
                    enemyBaseSpeed = 450f;
                    enemySpawnInterval = 0.15f;
                    player.BulletSpeed = 800f;
                    break;
                case Difficulty.SeriouslyInsane:
                    enemyBaseSpeed = 600f;
                    enemySpawnInterval = 0.1f;
                    player.BulletSpeed = 1000f;
                    break;
                case Difficulty.Unbeatable:
                    enemyBaseSpeed = 700f;
                    enemySpawnInterval = 0.08f;
                    player.BulletSpeed = 1000f;
                    break;
                case Difficulty.BlindNightmare:
                    enemyBaseSpeed = 600f;
                    enemySpawnInterval = 0.1f;
                    player.BulletSpeed = 1200f;
                    break;
            }

            player = new Player
            {
                X = gameSettings.Width / 2,
                Y = gameSettings.Height - 100,
                Size = 40,
                Health = (insaneMode || seriouslyInsaneMode || unbeatableMode || blindNightmare) ? 1 : 3,
                Speed = 400f,
                BulletSpeed = 800f,
                ShootCooldown = 0f
            };
            player.BulletSpeed = (currentDifficulty == Difficulty.Easy) ? 700f :
                                 (currentDifficulty == Difficulty.Medium) ? 600f :
                                 (currentDifficulty == Difficulty.Hard) ? 500f :
                                 (currentDifficulty == Difficulty.Insane) ? 800f :
                                 (currentDifficulty == Difficulty.SeriouslyInsane) ? 1000f :
                                 (currentDifficulty == Difficulty.Unbeatable) ? 1000f : 1200f;

            enemies.Clear();
            playerBullets.Clear();
            enemyBullets.Clear();
            powerUps.Clear();
            particles.Clear();
            scorePopups.Clear();
            enemySpawnTimer = 0f;
            powerUpSpawnTimer = 0f;
            enemySpeedMultiplier = 1f;
            spawnIntervalMultiplier = 1f;
            score = 0;
            rapidFireTimer = 0f;
            shieldTimer = 0f;
            scoreMultiplierTimer = 0f;
            shotCooldownTimer = 0f;

            Engine.DisableSpotlight();
            Engine.DisableDistortion();

            if (blindNightmare)
            {
                Engine.SetDistortion(15f, 3f);
            }

            // Start background music (looping)
            Engine.PlaySound("bg_music.wav", loop: true);

            currentState = GameState.Playing;
        }

        static void UpdatePlaying(float dt)
        {
            if (rapidFireTimer > 0) rapidFireTimer -= dt;
            if (shieldTimer > 0) shieldTimer -= dt;
            if (scoreMultiplierTimer > 0) scoreMultiplierTimer -= dt;

            if ((insaneMode || seriouslyInsaneMode || blindNightmare) && shotCooldownTimer > 0)
                shotCooldownTimer -= dt;

            float currentFireCooldown = (rapidFireTimer > 0) ? 0.075f : 0.15f;
            if (unbeatableMode) currentFireCooldown = 0.1f;
            player.ShootCooldown -= dt;

            if (Input.GetKeyDown(Keys.Space) && player.ShootCooldown <= 0)
            {
                bool canShoot = true;
                if ((insaneMode || seriouslyInsaneMode || blindNightmare) && shotCooldownTimer > 0)
                {
                    canShoot = false;
                    scorePopups.Add(new ScorePopup
                    {
                        X = player.X,
                        Y = player.Y - 30,
                        Text = $"WAIT {shotCooldownTimer:F1}s",
                        Life = 0.8f,
                        Color = Color.Red
                    });
                }

                if (canShoot)
                {
                    player.ShootCooldown = currentFireCooldown;
                    if (insaneMode || seriouslyInsaneMode)
                        shotCooldownTimer = shotCooldownDuration;
                    if (blindNightmare)
                        shotCooldownTimer = blindShotCooldown;

                    Bullet b = GetBullet();
                    b.Active = true;
                    b.X = player.X;
                    b.Y = player.Y - player.Size / 2 - 5;
                    b.IsEnemy = false;
                    playerBullets.Add(b);

                    Engine.PlaySound("bullet_shoot.wav");

                    Engine.SetAdditiveBlending(true);
                    for (int i = 0; i < 5; i++)
                    {
                        particles.Add(new Particle
                        {
                            X = player.X,
                            Y = player.Y - player.Size / 2,
                            VX = Engine.RandomFloat(-30, 30),
                            VY = Engine.RandomFloat(-80, -40),
                            Life = 0.3f,
                            Color = Color.FromArgb(200, 255, 255, 100)
                        });
                    }
                    Engine.SetAdditiveBlending(false);
                }
            }

            // Player movement
            float moveX = 0, moveY = 0;
            if (Input.GetKey(Keys.Left) || Input.GetKey(Keys.A)) moveX -= 1;
            if (Input.GetKey(Keys.Right) || Input.GetKey(Keys.D)) moveX += 1;
            if (Input.GetKey(Keys.Up) || Input.GetKey(Keys.W)) moveY -= 1;
            if (Input.GetKey(Keys.Down) || Input.GetKey(Keys.S)) moveY += 1;

            if (moveX != 0 || moveY != 0)
            {
                float len = (float)Math.Sqrt(moveX * moveX + moveY * moveY);
                moveX /= len;
                moveY /= len;
                player.VX = moveX * player.Speed;
                player.VY = moveY * player.Speed;
            }
            else
            {
                player.VX = player.VY = 0;
            }

            player.X += player.VX * dt;
            player.Y += player.VY * dt;
            player.X = Engine.Clamp(player.X, player.Size / 2, Engine.WindowWidth - player.Size / 2);
            player.Y = Engine.Clamp(player.Y, player.Size / 2, Engine.WindowHeight - player.Size / 2);

            if (blindNightmare)
            {
                Engine.EnableSpotlight(150f, new PointF(player.X, player.Y));
            }

            // Enemy spawning (wave counter removed)
            enemySpawnTimer += dt;
            while (enemySpawnTimer >= enemySpawnInterval * spawnIntervalMultiplier)
            {
                enemySpawnTimer -= enemySpawnInterval * spawnIntervalMultiplier;
                float x = Engine.RandomFloat(player.Size, Engine.WindowWidth - player.Size);
                enemies.Add(new Enemy
                {
                    X = x,
                    Y = -20,
                    Size = 35,
                    Speed = enemyBaseSpeed * enemySpeedMultiplier + Engine.RandomFloat(-30, 30),
                    Angle = 0,
                    ShootCooldown = Engine.RandomFloat(0.5f, 2f)
                });
            }

            // Power-up spawning (type 0-3)
            if (!insaneMode && !seriouslyInsaneMode && !unbeatableMode && !blindNightmare)
            {
                powerUpSpawnTimer += dt;
                while (powerUpSpawnTimer >= powerUpSpawnInterval)
                {
                    powerUpSpawnTimer -= powerUpSpawnInterval;
                    float x = Engine.RandomFloat(20, Engine.WindowWidth - 20);
                    int type = Engine.RandomInt(0, 4);
                    powerUps.Add(new PowerUp
                    {
                        X = x,
                        Y = -20,
                        Size = 16,
                        Type = type
                    });
                }
            }

            // Enemy movement and shooting
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var e = enemies[i];
                e.Y += e.Speed * dt;
                e.Angle += 360f * dt * 0.5f;

                if (insaneMode || seriouslyInsaneMode || unbeatableMode || blindNightmare)
                {
                    e.ShootCooldown -= dt;
                    float shootInterval = (unbeatableMode || blindNightmare) ? 0.5f : 1.5f;
                    if (e.ShootCooldown <= 0)
                    {
                        Bullet eb = GetEnemyBullet();
                        eb.Active = true;
                        eb.X = e.X;
                        eb.Y = e.Y + e.Size / 2;
                        eb.IsEnemy = true;
                        enemyBullets.Add(eb);
                        e.ShootCooldown = Engine.RandomFloat(shootInterval * 0.8f, shootInterval * 1.2f);
                    }
                }

                if (e.Y > Engine.WindowHeight + 30)
                {
                    enemies.RemoveAt(i);
                }
                else
                {
                    enemies[i] = e;
                }
            }

            // Player bullets update
            for (int i = playerBullets.Count - 1; i >= 0; i--)
            {
                var b = playerBullets[i];
                if (!b.Active) continue;
                b.Y -= player.BulletSpeed * dt;
                if (b.Y < -20)
                {
                    b.Active = false;
                    playerBullets.RemoveAt(i);
                    ReturnBulletToPool(b);
                }
                else
                {
                    bool hit = false;
                    for (int j = enemies.Count - 1; j >= 0; j--)
                    {
                        var e = enemies[j];
                        float dx = b.X - e.X;
                        float dy = b.Y - e.Y;
                        float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                        if (dist < (b.Size + e.Size) / 2)
                        {
                            enemies.RemoveAt(j);
                            hit = true;
                            int basePoints = 10;
                            if (e.Speed > enemyBaseSpeed * 1.5f) basePoints = 20;
                            else if (e.Speed > enemyBaseSpeed) basePoints = 15;
                            int points = (int)(basePoints * (scoreMultiplierTimer > 0 ? 2 : 1));
                            score += points;

                            Engine.PlaySound("explode.wav");

                            scorePopups.Add(new ScorePopup
                            {
                                X = e.X,
                                Y = e.Y - 10,
                                Text = $"+{points}",
                                Life = 1f,
                                Color = Color.Yellow
                            });

                            Engine.SetAdditiveBlending(true);
                            for (int k = 0; k < 15; k++)
                            {
                                particles.Add(new Particle
                                {
                                    X = e.X,
                                    Y = e.Y,
                                    VX = Engine.RandomFloat(-200, 200),
                                    VY = Engine.RandomFloat(-200, 200),
                                    Life = 0.8f,
                                    Color = Color.FromArgb(255, 255, 100, 0)
                                });
                            }
                            Engine.SetAdditiveBlending(false);

                            // Difficulty scaling (wave counter removed, but keep multiplier)
                            enemySpeedMultiplier = Math.Min(3f, enemySpeedMultiplier + 0.02f);
                            spawnIntervalMultiplier = Math.Max(0.3f, spawnIntervalMultiplier - 0.01f);
                            break;
                        }
                    }
                    if (hit)
                    {
                        b.Active = false;
                        playerBullets.RemoveAt(i);
                        ReturnBulletToPool(b);
                    }
                }
            }

            // Enemy bullets update
            for (int i = enemyBullets.Count - 1; i >= 0; i--)
            {
                var b = enemyBullets[i];
                if (!b.Active) continue;
                b.Y += (unbeatableMode || blindNightmare ? 600f : 400f) * dt;
                if (b.Y > Engine.WindowHeight + 20)
                {
                    b.Active = false;
                    enemyBullets.RemoveAt(i);
                    ReturnEnemyBulletToPool(b);
                }
                else
                {
                    if (shieldTimer <= 0)
                    {
                        float dx = b.X - player.X;
                        float dy = b.Y - player.Y;
                        float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                        if (dist < (b.Size + player.Size) / 2 && player.InvincibleTimer <= 0)
                        {
                            player.Health--;
                            player.InvincibleTimer = 1.2f;
                            b.Active = false;
                            enemyBullets.RemoveAt(i);
                            ReturnEnemyBulletToPool(b);

                            Engine.Shake(10f, 0.3f);

                            Engine.SetAdditiveBlending(true);
                            for (int k = 0; k < 15; k++)
                            {
                                particles.Add(new Particle
                                {
                                    X = b.X,
                                    Y = b.Y,
                                    VX = Engine.RandomFloat(-200, 200),
                                    VY = Engine.RandomFloat(-200, 200),
                                    Life = 1f,
                                    Color = Color.FromArgb(200, 255, 0, 255)
                                });
                            }
                            Engine.SetAdditiveBlending(false);

                            if (player.Health <= 0)
                            {
                                Engine.PlaySound("explode.wav");
                                currentState = GameState.GameOver;
                                if (score > highScore) highScore = score;
                            }
                        }
                    }
                }
            }

            // Power-ups update
            for (int i = powerUps.Count - 1; i >= 0; i--)
            {
                var p = powerUps[i];
                p.Y += 100f * dt;
                if (p.Y > Engine.WindowHeight + 30)
                {
                    powerUps.RemoveAt(i);
                }
                else
                {
                    float dx = p.X - player.X;
                    float dy = p.Y - player.Y;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (dist < (p.Size + player.Size) / 2)
                    {
                        switch (p.Type)
                        {
                            case 0:
                                player.Health = Math.Min((insaneMode || seriouslyInsaneMode || unbeatableMode || blindNightmare) ? 1 : 3, player.Health + 1);
                                scorePopups.Add(new ScorePopup { X = p.X, Y = p.Y, Text = "+HEALTH", Life = 1f, Color = Color.Green });
                                break;
                            case 1:
                                rapidFireTimer = rapidFireDuration;
                                scorePopups.Add(new ScorePopup { X = p.X, Y = p.Y, Text = "RAPID FIRE!", Life = 1f, Color = Color.Orange });
                                break;
                            case 2:
                                shieldTimer = shieldDuration;
                                scorePopups.Add(new ScorePopup { X = p.X, Y = p.Y, Text = "SHIELD!", Life = 1f, Color = Color.Cyan });
                                break;
                            case 3:
                                scoreMultiplierTimer = scoreMultiplierDuration;
                                scorePopups.Add(new ScorePopup { X = p.X, Y = p.Y, Text = "2X SCORE!", Life = 1f, Color = Color.Gold });
                                break;
                        }

                        for (int k = 0; k < 8; k++)
                        {
                            particles.Add(new Particle
                            {
                                X = p.X,
                                Y = p.Y,
                                VX = Engine.RandomFloat(-100, 100),
                                VY = Engine.RandomFloat(-100, 100),
                                Life = 0.6f,
                                Color = Color.White
                            });
                        }
                        powerUps.RemoveAt(i);
                    }
                }
            }

            // Player-enemy collision
            if (shieldTimer <= 0)
            {
                foreach (var e in enemies)
                {
                    float dx = e.X - player.X;
                    float dy = e.Y - player.Y;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);
                    if (dist < (e.Size + player.Size) / 2 && player.InvincibleTimer <= 0)
                    {
                        player.Health--;
                        player.InvincibleTimer = 1.2f;
                        enemies.Remove(e);

                        Engine.Shake(15f, 0.4f);

                        for (int k = 0; k < 20; k++)
                        {
                            particles.Add(new Particle
                            {
                                X = e.X,
                                Y = e.Y,
                                VX = Engine.RandomFloat(-300, 300),
                                VY = Engine.RandomFloat(-300, 300),
                                Life = 1f,
                                Color = Color.Red
                            });
                        }

                        if (player.Health <= 0)
                        {
                            Engine.PlaySound("explode.wav");
                            currentState = GameState.GameOver;
                            if (score > highScore) highScore = score;
                        }
                        break;
                    }
                }
            }

            if (player.InvincibleTimer > 0)
                player.InvincibleTimer -= dt;

            score += (int)(dt * 10);
            UpdateParticles(dt);
            UpdateScorePopups(dt);
        }

        static void UpdateGameOver(float dt)
        {
            if (Input.GetKeyDown(Keys.Space))
            {
                StartGame();
            }
            else if (Input.GetKeyDown(Keys.R))
            {
                Engine.StopSound("bg_music.wav");
                currentState = GameState.Menu;
            }
            UpdateParticles(dt);
            UpdateScorePopups(dt);
        }

        static void UpdateParticles(float dt)
        {
            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i];
                p.X += p.VX * dt;
                p.Y += p.VY * dt;
                p.Life -= dt * 2;
                if (p.Life <= 0)
                    particles.RemoveAt(i);
                else
                    particles[i] = p;
            }
        }

        static void UpdateScorePopups(float dt)
        {
            for (int i = scorePopups.Count - 1; i >= 0; i--)
            {
                var s = scorePopups[i];
                s.Y -= 30 * dt;
                s.Life -= dt * 2;
                if (s.Life <= 0)
                    scorePopups.RemoveAt(i);
                else
                    scorePopups[i] = s;
            }
        }

        static void UpdateStars(float dt)
        {
            foreach (var star in stars)
            {
                star.Y += star.Speed * dt;
                if (star.Y > Engine.WindowHeight)
                {
                    star.Y = 0;
                    star.X = Engine.RandomFloat(0, Engine.WindowWidth);
                }
            }
        }

        static void Draw()
        {
            Engine.Clear(Color.FromArgb(10, 10, 20));

            // Stars
            foreach (var star in stars)
            {
                int brightness = (int)(255 * star.Brightness);
                Color col = Color.FromArgb(brightness, brightness, brightness);
                Engine.DrawRect((int)star.X, (int)star.Y, star.Size, star.Size, col, true);
            }

            // Particles (additive blending)
            Engine.SetAdditiveBlending(true);
            foreach (var p in particles)
            {
                int alpha = (int)(255 * p.Life);
                Color col = Color.FromArgb(alpha, p.Color);
                Engine.DrawCircle((int)p.X, (int)p.Y, 3, col, true);
            }
            Engine.SetAdditiveBlending(false);

            // Power-ups
            foreach (var pu in powerUps)
            {
                PointF[] diamond = new PointF[]
                {
                    new PointF(pu.X, pu.Y - pu.Size/2),
                    new PointF(pu.X + pu.Size/2, pu.Y),
                    new PointF(pu.X, pu.Y + pu.Size/2),
                    new PointF(pu.X - pu.Size/2, pu.Y)
                };
                Color puColor;
                switch (pu.Type)
                {
                    case 0: puColor = Color.Green; break;
                    case 1: puColor = Color.Orange; break;
                    case 2: puColor = Color.Cyan; break;
                    case 3: puColor = Color.Gold; break;
                    default: puColor = Color.White; break;
                }
                Engine.DrawPolygon(diamond, puColor, filled: true);
            }

            // Player bullets
            foreach (var b in playerBullets)
            {
                if (b.Active)
                {
                    if (bulletSpriteId != -1)
                    {
                        Engine.DrawSprite(bulletSpriteId,
                            (int)(b.X - b.Size / 2),
                            (int)(b.Y - b.Size / 2),
                            (int)b.Size,
                            (int)b.Size);
                    }
                    else
                    {
                        Engine.DrawRect((int)(b.X - b.Size / 2), (int)(b.Y - b.Size / 2),
                                        (int)b.Size, (int)b.Size, Color.Yellow, true);
                    }
                }
            }

            // Enemy bullets
            foreach (var b in enemyBullets)
            {
                if (b.Active)
                {
                    Engine.DrawRect((int)(b.X - b.Size / 2), (int)(b.Y - b.Size / 2),
                                    (int)b.Size, (int)b.Size, Color.Magenta, true);
                }
            }

            // Enemies
            foreach (var e in enemies)
            {
                if (enemySpriteId != -1)
                {
                    Engine.DrawSprite(enemySpriteId,
                                      (int)(e.X - e.Size / 2),
                                      (int)(e.Y - e.Size / 2),
                                      (int)e.Size,
                                      (int)e.Size);
                }
                else
                {
                    PointF[] hex = new PointF[6];
                    for (int i = 0; i < 6; i++)
                    {
                        float angle = (float)(i * Math.PI / 3) + e.Angle * (float)(Math.PI / 180);
                        hex[i] = new PointF(
                            e.X + (float)Math.Cos(angle) * e.Size / 2,
                            e.Y + (float)Math.Sin(angle) * e.Size / 2);
                    }
                    Engine.DrawPolygon(hex, Color.Red, filled: true);
                }
            }

            // Player
            if (currentState == GameState.Playing)
            {
                bool drawPlayer = true;
                if (player.InvincibleTimer > 0 && (int)(player.InvincibleTimer * 10) % 2 == 0)
                    drawPlayer = false;
                if (shieldTimer > 0)
                {
                    Engine.DrawCircle((int)player.X, (int)player.Y, (int)(player.Size * 0.8f), Color.Cyan, false);
                }
                if (drawPlayer)
                {
                    if (playerSpriteId != -1)
                    {
                        Engine.DrawSprite(playerSpriteId,
                                          (int)(player.X - player.Size / 2),
                                          (int)(player.Y - player.Size / 2),
                                          (int)player.Size,
                                          (int)player.Size);
                    }
                    else
                    {
                        PointF[] triangle = new PointF[]
                        {
                            new PointF(player.X, player.Y - player.Size),
                            new PointF(player.X + player.Size * 0.8f, player.Y + player.Size * 0.5f),
                            new PointF(player.X - player.Size * 0.8f, player.Y + player.Size * 0.5f)
                        };
                        Engine.DrawPolygon(triangle, playerColor, filled: true);
                    }
                }
            }

            // Score popups
            foreach (var sp in scorePopups)
            {
                int alpha = (int)(255 * sp.Life);
                Color col = Color.FromArgb(alpha, sp.Color);
                Engine.DrawText(sp.Text, (int)sp.X, (int)sp.Y, col, 14, true);
            }

            // UI
            if (currentState == GameState.Menu)
            {
                int cx = Engine.WindowWidth / 2;
                Engine.DrawTextCentered("SPACALLAX 1.0.0", cx, 150, Color.Cyan, 48);
                Engine.DrawTextCentered("Select Difficulty:", cx, 250, Color.White, 24);
                string diffText = $"< {difficultyNames[selectedDifficultyIndex]} >";
                Engine.DrawTextCentered(diffText, cx, 300, Color.Yellow, 32);
                Engine.DrawTextCentered("Press SPACE or ENTER to start", cx, 400, Color.LightGray, 20);
                Engine.DrawTextCentered("Press S for Settings", cx, 450, Color.LightGray, 18);
                Engine.DrawText("F11: Fullscreen", 10, Engine.WindowHeight - 30, Color.Gray, 14);
            }
            else if (currentState == GameState.Playing)
            {
                // Top-left
                Engine.DrawText($"Score: {score}  High Score: {highScore}", 10, 10, Color.White, 20);
                string healthText = (insaneMode || seriouslyInsaneMode || unbeatableMode || blindNightmare) ? $"Health: {player.Health}/1" : $"Health: {player.Health}/3";
                Engine.DrawText(healthText, 10, 35, Color.LightGreen, 16);
                float fps = 1f / Engine.RawDeltaTime;
                Engine.DrawText($"FPS: {fps:F0}", 10, 60, Color.Yellow, 16);

                // Top-right
                string diffName = difficultyNames[(int)currentDifficulty];
                Engine.DrawText($"Difficulty: {diffName}", Engine.WindowWidth - 300, 10, Color.Orange, 16);
                if (insaneMode || seriouslyInsaneMode)
                {
                    if (shotCooldownTimer > 0)
                        Engine.DrawText($"Shot ready in: {shotCooldownTimer:F1}s", Engine.WindowWidth - 250, 35, Color.Magenta, 14);
                    else
                        Engine.DrawText($"Shot ready!", Engine.WindowWidth - 200, 35, Color.Green, 14);
                }
                if (blindNightmare)
                {
                    if (shotCooldownTimer > 0)
                        Engine.DrawText($"Blind shot: {shotCooldownTimer:F1}s", Engine.WindowWidth - 250, 35, Color.Magenta, 14);
                    else
                        Engine.DrawText($"Blind shot ready!", Engine.WindowWidth - 200, 35, Color.Green, 14);
                }
                if (unbeatableMode)
                {
                    Engine.DrawText("GOOD LUCK", Engine.WindowWidth - 200, 35, Color.Red, 16);
                }
                if (rapidFireTimer > 0)
                {
                    Engine.DrawText($"Rapid Fire: {rapidFireTimer:F1}s", Engine.WindowWidth - 200, 55, Color.Orange, 12);
                }
                if (shieldTimer > 0)
                {
                    Engine.DrawText($"Shield: {shieldTimer:F1}s", Engine.WindowWidth - 200, 70, Color.Cyan, 12);
                }
                if (scoreMultiplierTimer > 0)
                {
                    Engine.DrawText($"2X Score: {scoreMultiplierTimer:F1}s", Engine.WindowWidth - 200, 85, Color.Gold, 12);
                }

                Engine.DrawText("F11: Fullscreen", Engine.WindowWidth - 150, Engine.WindowHeight - 20, Color.Gray, 12);
            }
            else if (currentState == GameState.GameOver)
            {
                int cx = Engine.WindowWidth / 2;
                Engine.DrawTextCentered("GAME OVER", cx, 200, Color.Red, 48);
                Engine.DrawTextCentered($"Your Score: {score}  High Score: {highScore}", cx, 280, Color.White, 24);
                Engine.DrawTextCentered("Press SPACE to restart", cx, 330, Color.LightGreen, 20);
                Engine.DrawTextCentered("Press R to return to menu", cx, 370, Color.LightGray, 18);
            }
            else if (currentState == GameState.Settings)
            {
                DrawSettings();
            }
        }

        static void DrawSettings()
        {
            int cx = Engine.WindowWidth / 2;
            int startY = 150;
            int lineHeight = 40;

            Engine.DrawTextCentered("SETTINGS", cx, 80, Color.Cyan, 48);

            for (int i = 0; i < settingNames.Length; i++)
            {
                Color color = (i == selectedSettingIndex) ? Color.Yellow : Color.White;
                string valueText = "";

                switch (i)
                {
                    case 0:
                        valueText = resolutionOptions[selectedResolutionIndex];
                        break;
                    case 1:
                        valueText = gameSettings.Fullscreen ? "ON" : "OFF";
                        break;
                    case 2:
                        valueText = gameSettings.VSync ? "ON" : "OFF";
                        break;
                    case 3:
                        valueText = $"{(int)(gameSettings.MasterVolume * 100)}%";
                        break;
                    case 4:
                        valueText = $"{(int)(gameSettings.SoundVolume * 100)}%";
                        break;
                    case 5:
                        valueText = $"{(int)(gameSettings.MusicVolume * 100)}%";
                        break;
                    case 6:
                        valueText = "";
                        break;
                }

                string display = settingNames[i];
                if (!string.IsNullOrEmpty(valueText))
                    display += $": {valueText}";

                Engine.DrawTextCentered(display, cx, startY + i * lineHeight, color, 24);
            }

            Engine.DrawTextCentered("Use Arrow Keys to navigate, Left/Right to change", cx, startY + settingNames.Length * lineHeight + 40, Color.Gray, 16);
            Engine.DrawTextCentered("Escape to return to menu", cx, startY + settingNames.Length * lineHeight + 70, Color.Gray, 14);
        }

        static Bullet GetBullet()
        {
            if (bulletPool.Count > 0)
                return bulletPool.Pop();
            else
                return new Bullet { Size = 6, Active = true, IsEnemy = false };
        }

        static void ReturnBulletToPool(Bullet b)
        {
            b.Active = false;
            bulletPool.Push(b);
        }

        static Bullet GetEnemyBullet()
        {
            if (enemyBulletPool.Count > 0)
                return enemyBulletPool.Pop();
            else
                return new Bullet { Size = 6, Active = true, IsEnemy = true };
        }

        static void ReturnEnemyBulletToPool(Bullet b)
        {
            b.Active = false;
            enemyBulletPool.Push(b);
        }

        class Player
        {
            public float X, Y;
            public float VX, VY;
            public float Size = 20;
            public float Speed = 400f;
            public float BulletSpeed = 600f;
            public float ShootCooldown = 0f;
            public int Health = 3;
            public float InvincibleTimer = 0f;
        }

        struct Enemy
        {
            public float X, Y;
            public float Size;
            public float Speed;
            public float Angle;
            public float ShootCooldown;
        }

        class Bullet
        {
            public float X, Y;
            public float Size;
            public bool Active = true;
            public bool IsEnemy;
        }

        struct PowerUp
        {
            public float X, Y;
            public float Size;
            public int Type;
        }

        struct Particle
        {
            public float X, Y, VX, VY, Life;
            public Color Color;
        }

        struct ScorePopup
        {
            public float X, Y, Life;
            public string Text;
            public Color Color;
        }

        class Star
        {
            public float X, Y, Speed, Brightness;
            public int Size = 2;
        }
    }
}